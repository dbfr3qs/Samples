import Foundation
import CryptoKit

/// OAuth2 client with PKCE and DPoP support for IdentityServer
@available(iOS 15.0, macOS 12.0, *)
public final class OAuthClient: @unchecked Sendable {
    private let idpBaseURL: String
    private let clientId: String
    private let redirectUri: String
    private let tokenStorage: TokenStorage
    private let dpopKeyManager: DPoPKeyManager
    private let dpopProofGenerator: DPoPProofGenerator
    
    public init(
        idpBaseURL: String = "https://idp.dev.internal",
        clientId: String = "mobile-client",
        redirectUri: String = "com.idp.mobile://callback"
    ) {
        self.idpBaseURL = idpBaseURL
        self.clientId = clientId
        self.redirectUri = redirectUri
        self.tokenStorage = TokenStorage()
        self.dpopKeyManager = DPoPKeyManager()
        self.dpopProofGenerator = DPoPProofGenerator()
    }
    
    // MARK: - PKCE Flow
    
    /// Generate PKCE code verifier and challenge
    public func generatePKCE() -> (verifier: String, challenge: String) {
        let verifier = generateCodeVerifier()
        let challenge = generateCodeChallenge(from: verifier)
        return (verifier, challenge)
    }
    
    /// Build authorization URL for PKCE flow
    public func buildAuthorizationURL(codeChallenge: String, state: String) -> URL? {
        var components = URLComponents(string: "\(idpBaseURL)/connect/authorize")
        components?.queryItems = [
            URLQueryItem(name: "client_id", value: clientId),
            URLQueryItem(name: "redirect_uri", value: redirectUri),
            URLQueryItem(name: "response_type", value: "code"),
            URLQueryItem(name: "scope", value: "openid profile email api offline_access"),
            URLQueryItem(name: "code_challenge", value: codeChallenge),
            URLQueryItem(name: "code_challenge_method", value: "S256"),
            URLQueryItem(name: "state", value: state)
        ]
        return components?.url
    }
    
    /// Exchange authorization code for tokens with DPoP support
    public func exchangeCodeForTokens(
        code: String,
        codeVerifier: String,
        prfOutput: Data? = nil,
        credentialId: Data? = nil
    ) async throws -> TokenResponse {
        let url = URL(string: "\(idpBaseURL)/connect/token")!
        
        var request = URLRequest(url: url)
        request.httpMethod = "POST"
        request.setValue("application/x-www-form-urlencoded", forHTTPHeaderField: "Content-Type")
        
        let body = [
            "grant_type": "authorization_code",
            "code": code,
            "redirect_uri": redirectUri,
            "client_id": clientId,
            "code_verifier": codeVerifier
        ]
        
        print("🔐 [OAuth] Exchanging code for tokens")
        print("🔐 [OAuth] Code verifier: \(codeVerifier)")
        print("🔐 [OAuth] Code verifier length: \(codeVerifier.count)")
        
        // Add DPoP proof if PRF output and credential ID are available
        if let prfOutput = prfOutput, let credentialId = credentialId {
            do {
                let (privateKey, jwk, thumbprint) = try dpopKeyManager.getOrDeriveKey(
                    prfOutput: prfOutput,
                    credentialId: credentialId
                )
                
                let dpopProof = try dpopProofGenerator.generateProof(
                    privateKey: privateKey,
                    jwk: jwk,
                    httpMethod: "POST",
                    httpUri: url.absoluteString
                )
                
                request.setValue(dpopProof, forHTTPHeaderField: "DPoP")
                print("✅ [OAuth] Added DPoP proof to token request (thumbprint: \(thumbprint.prefix(20))...)")
            } catch {
                print("⚠️ [OAuth] Failed to generate DPoP proof: \(error), continuing without DPoP")
            }
        }
        
        request.httpBody = body.percentEncoded()
        
        let (data, response) = try await URLSession.shared.data(for: request)
        
        guard let httpResponse = response as? HTTPURLResponse,
              httpResponse.statusCode == 200 else {
            let errorBody = String(data: data, encoding: .utf8) ?? "Unknown error"
            print("❌ [OAuth] Token exchange failed: \(errorBody)")
            throw OAuthError.tokenExchangeFailed
        }
        
        let tokenResponse = try JSONDecoder().decode(TokenResponse.self, from: data)
        
        // Store tokens securely
        try tokenStorage.saveAccessToken(tokenResponse.accessToken)
        if let refreshToken = tokenResponse.refreshToken {
            try tokenStorage.saveRefreshToken(refreshToken)
        }
        if let idToken = tokenResponse.idToken {
            try tokenStorage.saveIdToken(idToken)
            print("✅ [OAuth] Stored ID token")
            print("🔍 [OAuth] ID token (first 100 chars): \(idToken.prefix(100))...")
            print("🔍 [OAuth] ID token length: \(idToken.count)")
        } else {
            print("⚠️ [OAuth] No ID token in response!")
        }
        if let expiresIn = tokenResponse.expiresIn {
            let expiry = Date().addingTimeInterval(TimeInterval(expiresIn))
            try tokenStorage.saveTokenExpiry(expiry)
        }
        
        return tokenResponse
    }
    
    /// Refresh access token using refresh token with DPoP support
    public func refreshAccessToken(credentialId: Data? = nil) async throws -> TokenResponse {
        guard let refreshToken = tokenStorage.getRefreshToken() else {
            throw OAuthError.noRefreshToken
        }
        
        let url = URL(string: "\(idpBaseURL)/connect/token")!
        
        var request = URLRequest(url: url)
        request.httpMethod = "POST"
        request.setValue("application/x-www-form-urlencoded", forHTTPHeaderField: "Content-Type")
        
        let body = [
            "grant_type": "refresh_token",
            "refresh_token": refreshToken,
            "client_id": clientId
        ]
        
        // Add DPoP proof if credential ID is available (uses cached key, no PRF prompt)
        if let credentialId = credentialId {
            do {
                let (privateKey, jwk, thumbprint) = try dpopKeyManager.getOrDeriveKey(
                    prfOutput: nil,
                    credentialId: credentialId
                )
                
                let dpopProof = try dpopProofGenerator.generateProof(
                    privateKey: privateKey,
                    jwk: jwk,
                    httpMethod: "POST",
                    httpUri: url.absoluteString
                )
                
                request.setValue(dpopProof, forHTTPHeaderField: "DPoP")
                print("✅ [OAuth] Added DPoP proof to refresh request (thumbprint: \(thumbprint.prefix(20))...)")
            } catch {
                print("⚠️ [OAuth] Failed to generate DPoP proof: \(error), continuing without DPoP")
            }
        }
        
        request.httpBody = body.percentEncoded()
        
        let (data, response) = try await URLSession.shared.data(for: request)
        
        guard let httpResponse = response as? HTTPURLResponse,
              httpResponse.statusCode == 200 else {
            let errorBody = String(data: data, encoding: .utf8) ?? "Unknown error"
            print("❌ [OAuth] Token refresh failed: \(errorBody)")
            throw OAuthError.refreshFailed
        }
        
        let tokenResponse = try JSONDecoder().decode(TokenResponse.self, from: data)
        
        // Update stored tokens
        try tokenStorage.saveAccessToken(tokenResponse.accessToken)
        if let newRefreshToken = tokenResponse.refreshToken {
            try tokenStorage.saveRefreshToken(newRefreshToken)
        }
        if let expiresIn = tokenResponse.expiresIn {
            let expiry = Date().addingTimeInterval(TimeInterval(expiresIn))
            try tokenStorage.saveTokenExpiry(expiry)
        }
        
        return tokenResponse
    }
    
    /// Get valid access token, refreshing if necessary
    public func getValidAccessToken(credentialId: Data? = nil) async throws -> String {
        if tokenStorage.isAccessTokenValid(), let token = tokenStorage.getAccessToken() {
            return token
        }
        
        // Token expired or missing, try to refresh
        let tokenResponse = try await refreshAccessToken(credentialId: credentialId)
        return tokenResponse.accessToken
    }
    
    /// Sign out and clear tokens
    public func signOut() {
        tokenStorage.clearTokens()
    }
    
    // MARK: - PKCE Helpers
    
    private func generateCodeVerifier() -> String {
        var bytes = [UInt8](repeating: 0, count: 32)
        _ = SecRandomCopyBytes(kSecRandomDefault, bytes.count, &bytes)
        return Data(bytes).base64URLEncodedString()
    }
    
    public func generateCodeChallenge(from verifier: String) -> String {
        guard let data = verifier.data(using: .utf8) else {
            fatalError("Failed to encode verifier")
        }
        let hash = SHA256.hash(data: data)
        return Data(hash).base64URLEncodedString()
    }
}

// MARK: - Models

public struct TokenResponse: Codable, Sendable {
    public let accessToken: String
    public let refreshToken: String?
    public let expiresIn: Int?
    public let tokenType: String
    public let scope: String?
    public let idToken: String?
    
    enum CodingKeys: String, CodingKey {
        case accessToken = "access_token"
        case refreshToken = "refresh_token"
        case expiresIn = "expires_in"
        case tokenType = "token_type"
        case scope
        case idToken = "id_token"
    }
}

public enum OAuthError: Error {
    case tokenExchangeFailed
    case refreshFailed
    case noRefreshToken
    case invalidResponse
}

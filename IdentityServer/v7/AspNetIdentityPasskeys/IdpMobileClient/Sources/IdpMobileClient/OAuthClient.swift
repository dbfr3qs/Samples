import Foundation
import CryptoKit

/// OAuth2 client with PKCE support for IdentityServer
public final class OAuthClient: @unchecked Sendable {
    private let idpBaseURL: String
    private let clientId: String
    private let redirectUri: String
    private let tokenStorage: TokenStorage
    
    public init(
        idpBaseURL: String = "https://idp.dev.internal",
        clientId: String = "mobile-client",
        redirectUri: String = "com.idp.mobile://callback"
    ) {
        self.idpBaseURL = idpBaseURL
        self.clientId = clientId
        self.redirectUri = redirectUri
        self.tokenStorage = TokenStorage()
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
    
    /// Exchange authorization code for tokens
    public func exchangeCodeForTokens(code: String, codeVerifier: String) async throws -> TokenResponse {
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
        
        request.httpBody = body.percentEncoded()
        
        let (data, response) = try await URLSession.shared.data(for: request)
        
        guard let httpResponse = response as? HTTPURLResponse,
              httpResponse.statusCode == 200 else {
            throw OAuthError.tokenExchangeFailed
        }
        
        let tokenResponse = try JSONDecoder().decode(TokenResponse.self, from: data)
        
        // Store tokens securely
        try tokenStorage.saveAccessToken(tokenResponse.accessToken)
        if let refreshToken = tokenResponse.refreshToken {
            try tokenStorage.saveRefreshToken(refreshToken)
        }
        if let expiresIn = tokenResponse.expiresIn {
            let expiry = Date().addingTimeInterval(TimeInterval(expiresIn))
            try tokenStorage.saveTokenExpiry(expiry)
        }
        
        return tokenResponse
    }
    
    /// Refresh access token using refresh token
    public func refreshAccessToken() async throws -> TokenResponse {
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
        
        request.httpBody = body.percentEncoded()
        
        let (data, response) = try await URLSession.shared.data(for: request)
        
        guard let httpResponse = response as? HTTPURLResponse,
              httpResponse.statusCode == 200 else {
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
    public func getValidAccessToken() async throws -> String {
        if tokenStorage.isAccessTokenValid(), let token = tokenStorage.getAccessToken() {
            return token
        }
        
        // Token expired or missing, try to refresh
        let tokenResponse = try await refreshAccessToken()
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
    
    enum CodingKeys: String, CodingKey {
        case accessToken = "access_token"
        case refreshToken = "refresh_token"
        case expiresIn = "expires_in"
        case tokenType = "token_type"
        case scope
    }
}

public enum OAuthError: Error {
    case tokenExchangeFailed
    case refreshFailed
    case noRefreshToken
    case invalidResponse
}

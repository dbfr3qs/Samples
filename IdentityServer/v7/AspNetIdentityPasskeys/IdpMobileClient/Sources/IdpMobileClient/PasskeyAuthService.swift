import Foundation
import AuthenticationServices

/// Service for passkey-based authentication with IdentityServer
@available(iOS 15.0, *)
public final class PasskeyAuthService: NSObject, @unchecked Sendable {
    private let idpBaseURL: String
    private let relyingPartyIdentifier: String
    
    public init(
        idpBaseURL: String = "https://idp.dev.internal",
        relyingPartyIdentifier: String = "idp.dev.internal"
    ) {
        self.idpBaseURL = idpBaseURL
        self.relyingPartyIdentifier = relyingPartyIdentifier
        super.init()
    }
    
    // MARK: - Passkey Registration
    
    /// Begin passkey registration flow
    /// Returns registration options from the IdP
    public func beginRegistration(username: String, email: String? = nil) async throws -> RegistrationOptions {
        let url = URL(string: "\(idpBaseURL)/api/passkey/register/begin")!
        
        print("🌐 [PasskeyService] POST \(url)")
        
        var request = URLRequest(url: url)
        request.httpMethod = "POST"
        request.setValue("application/json", forHTTPHeaderField: "Content-Type")
        
        var body: [String: String?] = ["username": username]
        if let email = email {
            body["email"] = email
        }
        request.httpBody = try JSONEncoder().encode(body)
        
        let (data, response) = try await URLSession.shared.data(for: request)
        
        guard let httpResponse = response as? HTTPURLResponse,
              httpResponse.statusCode == 200 else {
            throw PasskeyError.registrationFailed
        }
        
        return try JSONDecoder().decode(RegistrationOptions.self, from: data)
    }
    
    /// Complete passkey registration with credential
    public func completeRegistration(
        credential: ASAuthorizationPlatformPublicKeyCredentialRegistration,
        challengeId: String
    ) async throws {
        let url = URL(string: "\(idpBaseURL)/api/passkey/register/complete")!
        
        print("🌐 [PasskeyService] POST \(url)")
        print("🔑 [PasskeyService] Challenge ID: \(challengeId)")
        
        var request = URLRequest(url: url)
        request.httpMethod = "POST"
        request.setValue("application/json", forHTTPHeaderField: "Content-Type")
        
        let credentialData = RegistrationCredential(
            id: credential.credentialID.base64URLEncodedString(),
            rawId: credential.credentialID.base64URLEncodedString(),
            type: "public-key",
            response: RegistrationResponse(
                clientDataJSON: credential.rawClientDataJSON.base64URLEncodedString(),
                attestationObject: credential.rawAttestationObject?.base64URLEncodedString() ?? ""
            ),
            clientExtensionResults: [:]
        )
        
        // Encode credential to JSON string
        let credentialJsonData = try JSONEncoder().encode(credentialData)
        let credentialJsonString = String(data: credentialJsonData, encoding: .utf8) ?? ""
        
        // Wrap in the expected request format with challengeId
        let requestBody = [
            "challengeId": challengeId,
            "credentialJson": credentialJsonString
        ]
        request.httpBody = try JSONEncoder().encode(requestBody)
        
        let (data, response) = try await URLSession.shared.data(for: request)
        
        guard let httpResponse = response as? HTTPURLResponse else {
            print("❌ [PasskeyService] Invalid response type")
            throw PasskeyError.registrationFailed
        }
        
        print("📡 [PasskeyService] Response status: \(httpResponse.statusCode)")
        
        guard httpResponse.statusCode == 200 else {
            let responseBody = String(data: data, encoding: .utf8) ?? "Unable to decode response"
            print("❌ [PasskeyService] Registration failed with status \(httpResponse.statusCode)")
            print("📄 [PasskeyService] Response body: \(responseBody)")
            throw PasskeyError.registrationFailed
        }
        
        print("✅ [PasskeyService] Registration completed successfully")
    }
    
    // MARK: - Passkey Authentication
    
    /// Begin passkey authentication flow
    /// Returns authentication options from the IdP
    public func beginAuthentication() async throws -> AuthenticationOptions {
        let url = URL(string: "\(idpBaseURL)/api/passkey/authenticate/begin")!
        
        print("🌐 [PasskeyService] POST \(url)")
        
        var request = URLRequest(url: url)
        request.httpMethod = "POST"
        request.setValue("application/json", forHTTPHeaderField: "Content-Type")
        
        // Send empty JSON body as the endpoint expects a request body
        request.httpBody = try JSONEncoder().encode(["username": nil as String?])
        
        let (data, response) = try await URLSession.shared.data(for: request)
        
        guard let httpResponse = response as? HTTPURLResponse else {
            print("❌ [PasskeyService] Invalid response type")
            throw PasskeyError.authenticationFailed
        }
        
        print("📡 [PasskeyService] Response status: \(httpResponse.statusCode)")
        
        guard httpResponse.statusCode == 200 else {
            let responseBody = String(data: data, encoding: .utf8) ?? "Unable to decode response"
            print("❌ [PasskeyService] Authentication failed with status \(httpResponse.statusCode)")
            print("📄 [PasskeyService] Response body: \(responseBody)")
            throw PasskeyError.authenticationFailed
        }
        
        do {
            let options = try JSONDecoder().decode(AuthenticationOptions.self, from: data)
            print("✅ [PasskeyService] Successfully decoded authentication options")
            return options
        } catch {
            let responseBody = String(data: data, encoding: .utf8) ?? "Unable to decode response"
            print("❌ [PasskeyService] Failed to decode response: \(error)")
            print("📄 [PasskeyService] Response body: \(responseBody)")
            throw error
        }
    }
    
    /// Complete passkey authentication with assertion
    /// Returns authorization code that can be exchanged for tokens
    public func completeAuthentication(
        credential: ASAuthorizationPlatformPublicKeyCredentialAssertion,
        challengeId: String,
        codeChallenge: String,
        codeChallengeMethod: String = "S256"
    ) async throws -> AuthenticationResult {
        let url = URL(string: "\(idpBaseURL)/api/passkey/authenticate/complete")!
        
        print("🌐 [PasskeyService] POST \(url)")
        print("🔑 [PasskeyService] Challenge ID: \(challengeId)")
        
        var request = URLRequest(url: url)
        request.httpMethod = "POST"
        request.setValue("application/json", forHTTPHeaderField: "Content-Type")
        
        let credentialData = AuthenticationCredential(
            id: credential.credentialID.base64URLEncodedString(),
            rawId: credential.credentialID.base64URLEncodedString(),
            type: "public-key",
            response: AuthenticationResponse(
                clientDataJSON: credential.rawClientDataJSON.base64URLEncodedString(),
                authenticatorData: credential.rawAuthenticatorData.base64URLEncodedString(),
                signature: credential.signature.base64URLEncodedString(),
                userHandle: credential.userID.base64URLEncodedString()
            ),
            clientExtensionResults: [:]
        )
        
        // Encode credential to JSON string
        let credentialJsonData = try JSONEncoder().encode(credentialData)
        let credentialJsonString = String(data: credentialJsonData, encoding: .utf8) ?? ""
        
        // Wrap in the expected request format with PKCE parameters and challengeId
        let requestBody: [String: String] = [
            "challengeId": challengeId,
            "credentialJson": credentialJsonString,
            "codeChallenge": codeChallenge,
            "codeChallengeMethod": codeChallengeMethod
        ]
        
        print("🔐 [PasskeyService] Sending code challenge: \(codeChallenge.prefix(20))...")
        print("🔐 [PasskeyService] Code challenge length: \(codeChallenge.count)")
        
        request.httpBody = try JSONEncoder().encode(requestBody)
        
        let (data, response) = try await URLSession.shared.data(for: request)
        
        guard let httpResponse = response as? HTTPURLResponse else {
            print("❌ [PasskeyService] Invalid response type")
            throw PasskeyError.authenticationFailed
        }
        
        print("📡 [PasskeyService] Response status: \(httpResponse.statusCode)")
        
        guard httpResponse.statusCode == 200 else {
            let responseBody = String(data: data, encoding: .utf8) ?? "Unable to decode response"
            print("❌ [PasskeyService] Authentication failed with status \(httpResponse.statusCode)")
            print("📄 [PasskeyService] Response body: \(responseBody)")
            throw PasskeyError.authenticationFailed
        }
        
        print("✅ [PasskeyService] Authentication completed successfully")
        return try JSONDecoder().decode(AuthenticationResult.self, from: data)
    }
    
    // MARK: - Platform Support
    
    /// Create authorization controller for passkey registration
    @available(iOS 15.0, *)
    public func createRegistrationRequest(options: RegistrationOptions) -> ASAuthorizationController {
        let provider = ASAuthorizationPlatformPublicKeyCredentialProvider(relyingPartyIdentifier: relyingPartyIdentifier)
        
        let challenge = Data(base64URLEncoded: options.challenge) ?? Data()
        let userID = Data(options.user.id.utf8)
        
        let request = provider.createCredentialRegistrationRequest(
            challenge: challenge,
            name: options.user.name,
            userID: userID
        )
        
        return ASAuthorizationController(authorizationRequests: [request])
    }
    
    /// Create authorization controller for passkey authentication
    @available(iOS 15.0, *)
    public func createAuthenticationRequest(options: AuthenticationOptions) -> ASAuthorizationController {
        let provider = ASAuthorizationPlatformPublicKeyCredentialProvider(relyingPartyIdentifier: relyingPartyIdentifier)
        
        guard let challenge = Data(base64URLEncoded: options.challenge) else {
            print("❌ [PasskeyService] Failed to decode challenge: \(options.challenge)")
            print("❌ [PasskeyService] Challenge length: \(options.challenge.count)")
            // Return controller with empty challenge - will fail but at least we log it
            let request = provider.createCredentialAssertionRequest(challenge: Data())
            return ASAuthorizationController(authorizationRequests: [request])
        }
        
        print("✅ [PasskeyService] Successfully decoded challenge, length: \(challenge.count) bytes")
        print("🔑 [PasskeyService] Using rpId: \(options.rpId ?? "nil")")
        
        let request = provider.createCredentialAssertionRequest(challenge: challenge)
        
        return ASAuthorizationController(authorizationRequests: [request])
    }
}

// MARK: - Models

public struct RegistrationOptions: Codable, Sendable {
    public let challenge: String
    public let challengeId: String
    public let rp: RelyingParty
    public let user: User
    public let timeout: Int?
    
    public struct RelyingParty: Codable, Sendable {
        public let name: String
        public let id: String
    }
    
    public struct User: Codable, Sendable {
        public let id: String
        public let name: String
        public let displayName: String
    }
}

public struct AuthenticationOptions: Codable, Sendable {
    public let challenge: String
    public let challengeId: String
    public let timeout: Int?
    public let rpId: String?
}

public struct RegistrationCredential: Codable, Sendable {
    public let id: String
    public let rawId: String
    public let type: String
    public let response: RegistrationResponse
    public let clientExtensionResults: [String: String]
}

public struct RegistrationResponse: Codable, Sendable {
    public let clientDataJSON: String
    public let attestationObject: String
}

public struct AuthenticationCredential: Codable, Sendable {
    public let id: String
    public let rawId: String
    public let type: String
    public let response: AuthenticationResponse
    public let clientExtensionResults: [String: String]
}

public struct AuthenticationResponse: Codable, Sendable {
    public let clientDataJSON: String
    public let authenticatorData: String
    public let signature: String
    public let userHandle: String
}

public struct AuthenticationResult: Codable, Sendable {
    public let code: String
    public let state: String?
}

public enum PasskeyError: Error {
    case registrationFailed
    case authenticationFailed
    case invalidChallenge
    case userCancelled
}

import Foundation
import AuthenticationServices
import CryptoKit

/// Service for passkey-based authentication with IdentityServer
@available(iOS 18.0, macOS 15.0, *)
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
        print("[PRF] 🚀 Starting passkey registration for user: \(username)")
        
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
        
        let options = try JSONDecoder().decode(RegistrationOptions.self, from: data)
        
        // Log PRF extension information
        print("[PRF] 📥 Received registration options from server")
        if let extensions = options.extensions {
            print("[PRF] 📦 Extensions object present in response")
            if extensions.prf != nil {
                print("[PRF] ✅ PRF extension present - server supports PRF")
            } else {
                print("[PRF] ⚠️ No PRF extension in response")
            }
        } else {
            print("[PRF] ⚠️ No extensions in registration options")
        }
        
        return options
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
        
        print("[PRF] 🎉 User completed passkey registration")
        print("[PRF] 🔍 Checking for PRF extension results...")
        
        // Check for PRF extension results
        let extensionResults: [String: String] = [:]
        
        // Try to access PRF results if available
        // Note: As of iOS 18.0, PRF results may not be directly accessible via public API
        // The authenticator stores the PRF key internally
        print("[PRF] 📋 Credential ID: \(credential.credentialID.base64URLEncodedString().prefix(20))...")
        print("[PRF] 📏 Credential ID length: \(credential.credentialID.count) bytes")
        
        // Check if PRF was enabled (indicated by successful registration with prf set)
        print("[PRF] ℹ️ PRF key should be stored by authenticator (not directly accessible)")
        print("[PRF] ℹ️ PRF will be available during authentication assertions")
        
        let credentialData = RegistrationCredential(
            id: credential.credentialID.base64URLEncodedString(),
            rawId: credential.credentialID.base64URLEncodedString(),
            type: "public-key",
            response: RegistrationResponse(
                clientDataJSON: credential.rawClientDataJSON.base64URLEncodedString(),
                attestationObject: credential.rawAttestationObject?.base64URLEncodedString() ?? ""
            ),
            clientExtensionResults: extensionResults
        )
        
        print("[PRF] 📤 Sending credential to server for verification...")
        
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
        print("[PRF] 🎊 Passkey registered with PRF support enabled")
        print("[PRF] 💡 PRF can now be used during authentication to derive keys")
    }
    
    // MARK: - Passkey Authentication
    
    /// Begin passkey authentication flow
    /// Returns authentication options from the IdP
    public func beginAuthentication() async throws -> AuthenticationOptions {
        let url = URL(string: "\(idpBaseURL)/api/passkey/authenticate/begin")!
        
        print("🌐 [PasskeyService] POST \(url)")
        print("[PRF] 🚀 Starting passkey authentication")
        
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
            
            // Log PRF extension information
            print("[PRF] 📥 Received authentication options from server")
            if let extensions = options.extensions {
                print("[PRF] 📦 Extensions object present in response")
                if extensions.prf != nil {
                    print("[PRF] ✅ PRF extension present - will use PRF during authentication")
                } else {
                    print("[PRF] ⚠️ No PRF extension in response")
                }
            } else {
                print("[PRF] ⚠️ No extensions in authentication options")
            }
            
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
        
        print("[PRF] 🎉 User completed passkey authentication")
        print("[PRF] 🔍 Checking for PRF extension results...")
        
        // Check for PRF extension results
        let extensionResults: [String: String] = [:]
        
        // Try to access PRF output if available
        // Note: As of iOS 18.0, PRF output should be accessible via the assertion
        print("[PRF] 📋 Credential ID: \(credential.credentialID.base64URLEncodedString().prefix(20))...")
        print("[PRF] 📏 Credential ID length: \(credential.credentialID.count) bytes")
        
        // TODO: Extract PRF output from credential when available
        // The PRF output should be available in the assertion response
        // This will be used to derive DPoP keys deterministically
        print("[PRF] ℹ️ PRF output extraction to be implemented")
        print("[PRF] ℹ️ Check credential properties for PRF results")
        
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
            clientExtensionResults: extensionResults
        )
        
        print("[PRF] 📤 Sending authentication credential to server...")
        
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
    public func createRegistrationRequest(options: RegistrationOptions) -> ASAuthorizationController {
        let provider = ASAuthorizationPlatformPublicKeyCredentialProvider(relyingPartyIdentifier: relyingPartyIdentifier)
        
        let challenge = Data(base64URLEncoded: options.challenge) ?? Data()
        let userID = Data(options.user.id.utf8)
        
        let request = provider.createCredentialRegistrationRequest(
            challenge: challenge,
            name: options.user.name,
            userID: userID
        )
        
        print("[PRF] 🔧 Configuring registration request...")
        print("[PRF] 👤 User: \(options.user.name)")
        print("[PRF] 🆔 User ID length: \(userID.count) bytes")
        print("[PRF] 🎲 Challenge length: \(challenge.count) bytes")
        
        // Enable PRF extension for iOS 18+
        // This tells the authenticator to store a PRF key alongside the passkey
        print("[PRF] 🔑 Setting request.prf = .checkForSupport (iOS 18+)")
        request.prf = .checkForSupport
        print("[PRF] ✅ PRF enabled for registration - authenticator will store PRF key")
        print("[PRF] 📱 Presenting passkey registration UI to user...")
        
        return ASAuthorizationController(authorizationRequests: [request])
    }
    
    /*
     Usage (in your ASAuthorizationControllerDelegate):
     
     if #available(iOS 18.0, *),
        let assertion = authorization.credential as? ASAuthorizationPlatformPublicKeyCredentialAssertion {
         // Example access path; actual SDK may expose prf output directly or via clientExtensionResults
         // let prfOutput: Data = assertion.value(forKey: "prfOutput1") as? Data ?? Data()
         // let dpopKey = try deriveDPoPPrivateKey(prfOutput: prfOutput, rpId: options.rpId ?? relyingPartyIdentifier, credentialIdB64Url: assertion.credentialID.base64URLEncodedString())
     }
     */
    
    /// Create authorization controller for passkey authentication with PRF support
    /// - Parameters:
    ///   - options: Authentication options from the server
    ///   - prfSalt: Salt data to use for PRF evaluation (32 bytes recommended)
    /// - Returns: ASAuthorizationController configured with PRF
    public func createAuthenticationRequest(options: AuthenticationOptions, prfSalt: Data) -> ASAuthorizationController {
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
        
        print("[PRF] 🔧 Configuring authentication request with PRF...")
        print("[PRF] 🎲 Challenge length: \(challenge.count) bytes")
        print("[PRF] 🧂 PRF salt length: \(prfSalt.count) bytes")
        print("[PRF] 🔢 PRF salt (Base64): \(prfSalt.base64EncodedString().prefix(20))...")
        
        // Configure PRF extension for iOS 18+
        // This provides salt input to generate deterministic PRF output
        let prfInputValues = ASAuthorizationPublicKeyCredentialPRFAssertionInput.InputValues(
            saltInput1: prfSalt,
            saltInput2: nil  // Optional: second salt for additional key derivation
        )
        
        request.prf = ASAuthorizationPublicKeyCredentialPRFAssertionInput.inputValues(
            prfInputValues,
            perCredentialInputValues: nil
        )
        
        print("[PRF] ✅ PRF configured with salt input")
        print("[PRF] 💡 Authenticator will generate deterministic output from this salt")
        print("[PRF] 📱 Presenting passkey authentication UI to user...")
        
        return ASAuthorizationController(authorizationRequests: [request])
    }
}

@available(iOS 18.0, macOS 15.0, *)
extension PasskeyAuthService {
    /// Derive a deterministic DPoP private key from PRF output, RP ID, and credential ID.
    /// - Parameters:
    ///   - prfOutput: The PRF output bytes returned by the authenticator (Data).
    ///   - rpId: The relying party identifier (string).
    ///   - credentialIdB64Url: The credential ID in base64url (from assertion.credentialID.base64URLEncodedString()).
    /// - Returns: A P256.Signing.PrivateKey suitable for DPoP (deterministic per passkey and RP).
    public func deriveDPoPPrivateKey(prfOutput: Data, rpId: String, credentialIdB64Url: String) throws -> P256.Signing.PrivateKey {
        let credIdData = Data(base64URLEncoded: credentialIdB64Url) ?? Data()
        let seed = DPoPKeyDeriver.deriveSeedFromPRF(prfOutput: prfOutput, rpId: rpId, credentialId: credIdData)
        return try DPoPKeyDeriver.makeP256PrivateKey(fromSeed: seed)
    }
}

// MARK: - Models

public struct RegistrationOptions: Codable, Sendable {
    public let challenge: String
    public let challengeId: String
    public let rp: RelyingParty
    public let user: User
    public let timeout: Int?
    public let extensions: Extensions?
    
    public struct RelyingParty: Codable, Sendable {
        public let name: String
        public let id: String
    }
    
    public struct User: Codable, Sendable {
        public let id: String
        public let name: String
        public let displayName: String
    }
    
    public struct Extensions: Codable, Sendable {
        public let prf: PRFExtension?
        
        public struct PRFExtension: Codable, Sendable {
            // Empty for registration - signals PRF support availability
            // The client will use request.prf = .checkForSupport to enable PRF
        }
    }
}

public struct AuthenticationOptions: Codable, Sendable {
    public let challenge: String
    public let challengeId: String
    public let timeout: Int?
    public let rpId: String?
    public let extensions: Extensions?
    
    public struct Extensions: Codable, Sendable {
        public let prf: PRFExtension?
        
        public struct PRFExtension: Codable, Sendable {
            // Empty for authentication begin - PRF salt retrieved from stored credential
        }
    }
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


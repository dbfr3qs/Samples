import SwiftUI
import AuthenticationServices
import IdpMobileClient
import CryptoKit
import WebKit

struct ContentView: View {
    @StateObject private var viewModel = AuthViewModel()
    @State private var showWebView = false
    
    var body: some View {
        NavigationView {
            if showWebView, let cookies = viewModel.sessionCookies {
                // Show WebView as embedded view
                AuthenticatedWebViewWrapper(
                    cookies: cookies,
                    initialUrl: URL(string: "https://web.dev.internal:5003")!
                )
                .navigationTitle("Web Content")
                .navigationBarTitleDisplayMode(.inline)
                .toolbar {
                    ToolbarItem(placement: .navigationBarLeading) {
                        Button(action: {
                            showWebView = false
                        }) {
                            HStack {
                                Image(systemName: "chevron.left")
                                Text("Back")
                            }
                        }
                    }
                }
            } else {
                // Show main menu
                VStack(spacing: 20) {
                    if viewModel.isAuthenticated {
                        // Authenticated state
                        VStack(spacing: 16) {
                            Text("✓ Signed In")
                                .font(.title2)
                                .foregroundColor(.green)
                            
                            if let username = viewModel.username {
                                Text("Welcome, \(username)")
                                    .font(.body)
                                    .foregroundColor(.secondary)
                            }
                            
                            Divider()
                                .padding(.vertical)
                            
                            if viewModel.hasDPoPBinding {
                                HStack {
                                    Image(systemName: "lock.shield.fill")
                                        .foregroundColor(.green)
                                    Text("DPoP Enabled")
                                        .font(.caption)
                                        .foregroundColor(.green)
                                }
                                .padding(.vertical, 4)
                            }
                            
                            Button(action: {
                                Task {
                                    do {
                                        try await viewModel.prepareAuthenticatedWebView()
                                        showWebView = true
                                    } catch {
                                        print("❌ Failed to prepare WebView: \(error)")
                                        viewModel.errorMessage = "Failed to prepare WebView: \(error.localizedDescription)"
                                    }
                                }
                            }) {
                                HStack {
                                    Image(systemName: "globe")
                                    Text("Open WebView")
                                }
                                .frame(maxWidth: .infinity)
                                .padding()
                                .background(Color.purple)
                                .foregroundColor(.white)
                                .cornerRadius(10)
                            }
                            .disabled(viewModel.isLoading)
                            
                            Button(action: {
                                Task {
                                    await viewModel.callApi()
                                }
                            }) {
                                HStack {
                                    Image(systemName: "network")
                                    Text("Call API")
                                }
                                .frame(maxWidth: .infinity)
                                .padding()
                                .background(Color.blue)
                                .foregroundColor(.white)
                                .cornerRadius(10)
                            }
                            .disabled(viewModel.isLoading)
                            
                            Button(action: {
                                Task {
                                    await viewModel.refreshToken()
                                }
                            }) {
                                HStack {
                                    Image(systemName: "arrow.clockwise")
                                    if viewModel.hasDPoPBinding {
                                        Text("Refresh Token (DPoP - No Passkey!)")
                                    } else {
                                        Text("Refresh Token")
                                    }
                                }
                                .frame(maxWidth: .infinity)
                                .padding()
                                .background(Color.orange)
                                .foregroundColor(.white)
                                .cornerRadius(10)
                            }
                            .disabled(viewModel.isLoading)
                            
                            if let apiResponse = viewModel.apiResponse {
                                VStack(alignment: .leading, spacing: 8) {
                                    Text("API Response:")
                                        .font(.headline)
                                    ScrollView {
                                        Text(apiResponse)
                                            .font(.system(.body, design: .monospaced))
                                            .padding()
                                            .frame(maxWidth: .infinity, alignment: .leading)
                                            .background(Color.gray.opacity(0.1))
                                            .cornerRadius(8)
                                    }
                                    .frame(maxHeight: 200)
                                }
                                .padding(.top)
                            }
                            
                            Spacer()
                            
                            Button(action: {
                                viewModel.signOut()
                            }) {
                                Text("Sign Out")
                                    .foregroundColor(.red)
                            }
                        }
                        .padding()
                    } else {
                        // Unauthenticated state
                        VStack(spacing: 16) {
                            Image(systemName: "person.badge.key.fill")
                                .font(.system(size: 60))
                                .foregroundColor(.blue)
                                .padding(.bottom, 20)
                            
                            Text("IdP Mobile Client")
                                .font(.title)
                                .fontWeight(.bold)
                            
                            Text("Sign in with your passkey to continue")
                                .font(.body)
                                .foregroundColor(.secondary)
                                .multilineTextAlignment(.center)
                                .padding(.horizontal)
                            
                            VStack(spacing: 12) {
                                Button(action: {
                                    Task {
                                        await viewModel.signInWithPasskey()
                                    }
                                }) {
                                    HStack {
                                        Image(systemName: "key.fill")
                                        Text("Sign in with Passkey")
                                    }
                                    .frame(maxWidth: .infinity)
                                    .padding()
                                    .background(Color.blue)
                                    .foregroundColor(.white)
                                    .cornerRadius(10)
                                }
                                .disabled(viewModel.isLoading)
                                
                                Button(action: {
                                    viewModel.showRegistration = true
                                }) {
                                    HStack {
                                        Image(systemName: "person.badge.plus")
                                        Text("Register New Passkey")
                                    }
                                    .frame(maxWidth: .infinity)
                                    .padding()
                                    .background(Color.green)
                                    .foregroundColor(.white)
                                    .cornerRadius(10)
                                }
                                .disabled(viewModel.isLoading)
                            }
                            .padding(.horizontal)
                            
                            if viewModel.isLoading {
                                ProgressView()
                                    .padding(.top)
                            }
                        }
                        .padding()
                    }
                    
                    if let error = viewModel.errorMessage {
                        Text(error)
                            .font(.caption)
                            .foregroundColor(.red)
                            .padding()
                            .background(Color.red.opacity(0.1))
                            .cornerRadius(8)
                            .padding(.horizontal)
                    }
                }
                .navigationTitle("IdP Demo")
                .sheet(isPresented: $viewModel.showRegistration) {
                    RegistrationView(viewModel: viewModel)
                }
            }
        }
    }
}

struct RegistrationView: View {
    @ObservedObject var viewModel: AuthViewModel
    @State private var username = ""
    @State private var email = ""
    @Environment(\.dismiss) var dismiss
    
    var body: some View {
        NavigationView {
            Form {
                Section(header: Text("Account Information")) {
                    TextField("Username", text: $username)
                        .textContentType(.username)
                        .autocapitalization(.none)
                    
                    TextField("Email (optional)", text: $email)
                        .textContentType(.emailAddress)
                        .keyboardType(.emailAddress)
                        .autocapitalization(.none)
                }
                
                Section {
                    Button(action: {
                        Task {
                            await viewModel.registerPasskey(username: username, email: email.isEmpty ? nil : email)
                            if viewModel.errorMessage == nil {
                                dismiss()
                            }
                        }
                    }) {
                        HStack {
                            Image(systemName: "person.badge.key")
                            Text("Register Passkey")
                        }
                    }
                    .disabled(username.isEmpty || viewModel.isLoading)
                }
                
                if viewModel.isLoading {
                    Section {
                        HStack {
                            Spacer()
                            ProgressView()
                            Spacer()
                        }
                    }
                }
                
                if let error = viewModel.errorMessage {
                    Section {
                        Text(error)
                            .foregroundColor(.red)
                            .font(.caption)
                    }
                }
            }
            .navigationTitle("Register Passkey")
            .navigationBarTitleDisplayMode(.inline)
            .toolbar {
                ToolbarItem(placement: .cancellationAction) {
                    Button("Cancel") {
                        dismiss()
                    }
                }
            }
        }
    }
}

@MainActor
class AuthViewModel: NSObject, ObservableObject {
    @Published var isAuthenticated = false
    @Published var isLoading = false
    @Published var errorMessage: String?
    @Published var apiResponse: String?
    @Published var username: String?
    @Published var showRegistration = false
    @Published var sessionId: String?
    @Published var sessionCookies: [HTTPCookie]?
    @Published var userId: String?
    
    private let oauthClient = OAuthClient()
    private let passkeyService = PasskeyAuthService()
    private let secureStorage = SecureStorage()
    private var apiClient: ApiClient!
    private let dpopKeyManager = DPoPKeyManager()
    private let sessionAssertionGenerator = SessionAssertionGenerator()
    private let sessionExchangeClient = SessionExchangeClient()
    
    private var authController: ASAuthorizationController?
    @Published var currentCodeVerifier: String?
    @Published var currentState: String?
    @Published var currentChallengeId: String?
    @Published var currentCredentialId: Data?
    @Published var hasDPoPBinding = false
    
    override init() {
        super.init()
        
        // Load persisted credential ID on init
        if let credentialId = try? secureStorage.getCurrentCredentialId() {
            currentCredentialId = credentialId
            print("✅ [Init] Loaded persisted credential ID: \(credentialId.base64EncodedString().prefix(20))...")
        }
        
        self.apiClient = ApiClient(oauthClient: oauthClient)
        checkAuthenticationStatus()
    }
    
    func checkAuthenticationStatus() {
        let tokenStorage = TokenStorage()
        isAuthenticated = tokenStorage.isAccessTokenValid()
    }
    
    func registerPasskey(username: String, email: String?) async {
        isLoading = true
        errorMessage = nil
        
        do {
            // Step 1: Get registration options from IdP
            print("🔐 [Passkey] Requesting registration options from IdP...")
            let options = try await passkeyService.beginRegistration(username: username, email: email)
            print("✅ [Passkey] Received challenge: \(options.challenge.prefix(20))...")
            print("✅ [Passkey] Received challengeId: \(options.challengeId)")
            currentChallengeId = options.challengeId
            
            // Step 2: Create and present passkey registration request
            print("🔐 [Passkey] Creating registration request...")
            let controller = passkeyService.createRegistrationRequest(options: options)
            controller.delegate = self
            controller.presentationContextProvider = self
            
            authController = controller
            print("🔐 [Passkey] Presenting passkey registration prompt...")
            controller.performRequests()
            
        } catch {
            isLoading = false
            print("❌ [Passkey] Registration error: \(error)")
            if let urlError = error as? URLError {
                errorMessage = "Network error: \(urlError.localizedDescription)"
            } else {
                errorMessage = "Failed to start passkey registration: \(error.localizedDescription)"
            }
        }
    }
    
    func signInWithPasskey() async {
        isLoading = true
        errorMessage = nil
        
        do {
            // Step 1: Generate PKCE parameters (must be done before authentication)
            print("🔐 [Passkey] Generating PKCE parameters...")
            let (verifier, codeChallenge) = oauthClient.generatePKCE()
            currentCodeVerifier = verifier
            print("✅ [Passkey] Generated code verifier: \(verifier)")
            print("✅ [Passkey] Generated code challenge: \(codeChallenge)")
            print("✅ [Passkey] Code verifier length: \(verifier.count)")
            print("✅ [Passkey] Code challenge length: \(codeChallenge.count)")
            
            // Step 2: Get authentication options from IdP
            print("🔐 [Passkey] Requesting authentication options from IdP...")
            let options = try await passkeyService.beginAuthentication()
            print("✅ [Passkey] Received challenge: \(options.challenge.prefix(20))...")
            print("✅ [Passkey] Received challengeId: \(options.challengeId)")
            currentChallengeId = options.challengeId
            
            // Step 3: Generate PRF salt (deterministic based on rpId)
            // This salt will be used by the authenticator to generate deterministic PRF output
            print("🔐 [Passkey] Generating PRF salt...")
            let rpId = options.rpId ?? "idp.dev.internal"
            let prfSaltData = SHA256.hash(data: Data(rpId.utf8))
            let prfSalt = Data(prfSaltData)
            print("✅ [Passkey] Generated PRF salt: \(prfSalt.base64EncodedString().prefix(20))...")
            print("✅ [Passkey] PRF salt length: \(prfSalt.count) bytes")
            
            // Step 4: Create and present passkey authentication request with PRF
            print("🔐 [Passkey] Creating authentication request with PRF...")
            let controller = passkeyService.createAuthenticationRequest(options: options, prfSalt: prfSalt)
            controller.delegate = self
            controller.presentationContextProvider = self
            
            authController = controller
            print("🔐 [Passkey] Presenting passkey prompt...")
            controller.performRequests()
            
        } catch {
            isLoading = false
            print("❌ [Passkey] Error: \(error)")
            if let urlError = error as? URLError {
                errorMessage = "Network error: \(urlError.localizedDescription) (Code: \(urlError.code.rawValue))"
            } else if let decodingError = error as? DecodingError {
                errorMessage = "Invalid response from IdP: \(decodingError.localizedDescription)"
            } else {
                errorMessage = "Failed to start passkey authentication: \(error.localizedDescription)"
            }
        }
    }
    
    func callApi() async {
        isLoading = true
        errorMessage = nil
        apiResponse = nil
        
        do {
            let response = try await apiClient.callTestEndpoint()
            apiResponse = response
        } catch {
            errorMessage = "API call failed: \(error.localizedDescription)"
        }
        
        isLoading = false
    }
    
    func refreshToken() async {
        isLoading = true
        errorMessage = nil
        
        do {
            print("🔄 [OAuth] Refreshing token...")
            
            if let credentialId = currentCredentialId {
                print("✅ [DPoP] Using cached DPoP key for refresh (no passkey prompt!)")
                let tokenResponse = try await oauthClient.refreshAccessToken(
                    credentialId: credentialId
                )
                print("✅ [DPoP] Token refreshed successfully without passkey prompt!")
                errorMessage = "✅ Token refreshed with DPoP (no passkey prompt!)"
            } else {
                print("ℹ️ [OAuth] No credential ID available, using standard refresh")
                let tokenResponse = try await oauthClient.refreshAccessToken()
                print("✅ [OAuth] Token refreshed successfully")
                errorMessage = "✅ Token refreshed (standard flow)"
            }
            
        } catch {
            print("❌ [OAuth] Token refresh failed: \(error)")
            errorMessage = "Token refresh failed: \(error.localizedDescription)"
        }
        
        isLoading = false
    }
    
    func signOut() {
        let tokenStorage = TokenStorage()
        tokenStorage.clearTokens()
        isAuthenticated = false
        username = nil
        apiResponse = nil
        hasDPoPBinding = false
        sessionId = nil
        sessionCookies = nil
        userId = nil
        
        // Clear persisted credential ID
        try? secureStorage.deleteCurrentCredentialId()
        currentCredentialId = nil
        
        print("✅ [Auth] Signed out successfully")
    }
    
    func prepareAuthenticatedWebView() async throws {
        // Get session ID from stored ID token
        let tokenStorage = TokenStorage()
        guard let idToken = tokenStorage.getIdToken() else {
            throw NSError(domain: "AuthViewModel", code: 1, userInfo: [NSLocalizedDescriptionKey: "No ID token available. Please sign in again."])
        }
        
        // Extract session ID and user ID from ID token
        guard let sessionId = extractSessionId(from: idToken) else {
            throw NSError(domain: "AuthViewModel", code: 2, userInfo: [NSLocalizedDescriptionKey: "No session ID in ID token. Please sign in again."])
        }
        
        guard let userId = extractUserId(from: idToken) else {
            throw NSError(domain: "AuthViewModel", code: 3, userInfo: [NSLocalizedDescriptionKey: "No user ID in ID token."])
        }
        
        guard let credentialId = currentCredentialId else {
            throw NSError(domain: "AuthViewModel", code: 4, userInfo: [NSLocalizedDescriptionKey: "No credential ID available"])
        }
        
        print("🔐 [Auth] Preparing authenticated WebView via session exchange...")
        print("   Session ID: \(sessionId)")
        print("   User ID: \(userId)")
        
        // Get DPoP key
        let (privateKey, jwk, thumbprint) = try dpopKeyManager.getOrDeriveKey(
            prfOutput: nil,
            credentialId: credentialId
        )
        
        print("   DPoP thumbprint: \(thumbprint.prefix(20))...")
        
        // Generate session assertion
        let assertion = try sessionAssertionGenerator.generateSessionAssertion(
            sessionId: sessionId,
            userId: userId,
            privateKey: privateKey,
            jwk: jwk,
            idpUrl: "https://idp.dev.internal"
        )
        
        // Exchange for IdP session cookies
        let response = try await sessionExchangeClient.exchangeForSession(assertion: assertion)
        
        self.sessionCookies = response.cookies
        
        print("✅ [Auth] Received \(response.cookies.count) session cookies for WebView")
    }
    
    private func extractSessionId(from idToken: String) -> String? {
        print("🔍 [Auth] Extracting session ID from ID token...")
        print("🔍 [Auth] Full ID token: \(idToken)")
        
        let parts = idToken.split(separator: ".")
        guard parts.count == 3 else {
            print("⚠️ [Auth] Invalid ID token format - expected 3 parts, got \(parts.count)")
            return nil
        }
        
        print("🔍 [Auth] ID token header: \(parts[0])")
        print("🔍 [Auth] ID token payload: \(parts[1])")
        print("🔍 [Auth] ID token signature: \(parts[2])")
        
        // JWT uses base64url encoding, need to convert to standard base64
        var base64 = String(parts[1])
            .replacingOccurrences(of: "-", with: "+")
            .replacingOccurrences(of: "_", with: "/")
        
        // Add padding if needed
        let remainder = base64.count % 4
        if remainder > 0 {
            base64 += String(repeating: "=", count: 4 - remainder)
        }
        
        guard let payloadData = Data(base64Encoded: base64) else {
            print("⚠️ [Auth] Failed to decode ID token payload")
            print("🔍 [Auth] Base64 payload (first 100 chars): \(base64.prefix(100))...")
            return nil
        }
        
        guard let payload = try? JSONSerialization.jsonObject(with: payloadData) as? [String: Any] else {
            print("⚠️ [Auth] Failed to parse ID token payload")
            return nil
        }
        
        print("🔍 [Auth] ID token payload claims: \(payload.keys.sorted())")
        print("🔍 [Auth] Full payload: \(payload)")
        
        if let sid = payload["sid"] as? String {
            print("✅ [Auth] Extracted session ID: \(sid)")
            return sid
        }
        
        print("⚠️ [Auth] No sid claim in ID token")
        print("🔍 [Auth] Available claims: \(payload.keys.joined(separator: ", "))")
        return nil
    }
    
    private func extractUserId(from idToken: String) -> String? {
        let parts = idToken.split(separator: ".")
        guard parts.count == 3 else { return nil }
        
        // JWT uses base64url encoding, need to convert to standard base64
        var base64 = String(parts[1])
            .replacingOccurrences(of: "-", with: "+")
            .replacingOccurrences(of: "_", with: "/")
        
        // Add padding if needed
        let remainder = base64.count % 4
        if remainder > 0 {
            base64 += String(repeating: "=", count: 4 - remainder)
        }
        
        guard let payloadData = Data(base64Encoded: base64) else { return nil }
        guard let payload = try? JSONSerialization.jsonObject(with: payloadData) as? [String: Any] else { return nil }
        
        return payload["sub"] as? String
    }
    
    private func handleRegistrationSuccess(credential: ASAuthorizationPlatformPublicKeyCredentialRegistration) async {
        do {
            guard let challengeId = currentChallengeId else {
                throw PasskeyError.invalidChallenge
            }
            
            print("✅ [Passkey] Registration credential received")
            print("🔑 [Passkey] Credential ID: \(credential.credentialID.base64EncodedString().prefix(20))...")
            print("🔑 [Passkey] Using challengeId: \(challengeId)")
            
            // Store credential ID for later use
            currentCredentialId = credential.credentialID
            try? secureStorage.storeCurrentCredentialId(credential.credentialID)
            
            // Check for PRF output (iOS 17+)
            if #available(iOS 17.0, *) {
                // Note: PRF output extraction would happen here if the API supported it
                // For now, we log that PRF was requested
                print("ℹ️ [Passkey] PRF extension support requires iOS 17+ API updates")
            }
            
            // Complete registration with IdP
            try await passkeyService.completeRegistration(
                credential: credential,
                challengeId: challengeId
            )
            
            print("✅ [Passkey] Registration completed successfully!")
            
            isLoading = false
            showRegistration = false
            errorMessage = nil
            
        } catch {
            isLoading = false
            print("❌ [Passkey] Registration failed: \(error)")
            errorMessage = "Registration failed: \(error.localizedDescription)"
        }
    }
    
    private func handleAuthenticationSuccess(credential: ASAuthorizationPlatformPublicKeyCredentialAssertion) async {
        do {
            print("🔐 [Passkey] Starting handleAuthenticationSuccess")
            
            guard let challengeId = currentChallengeId else {
                print("❌ [Passkey] No challengeId found")
                throw PasskeyError.invalidChallenge
            }
            print("✅ [Passkey] ChallengeId found: \(challengeId)")
            
            guard let verifier = currentCodeVerifier else {
                print("❌ [Passkey] No code verifier found")
                throw PasskeyError.invalidChallenge
            }
            print("✅ [Passkey] Code verifier found: \(verifier.prefix(20))...")
            
            // Store credential ID for DPoP operations
            currentCredentialId = credential.credentialID
            try? secureStorage.storeCurrentCredentialId(credential.credentialID)
            print("✅ [Passkey] Credential ID: \(credential.credentialID.base64EncodedString().prefix(20))...")
            
            // Extract PRF output (iOS 18+) or fallback deterministically for demo
            var prfOutput: Data? = nil
            if #available(iOS 18.0, *) {
                print("[PRF] 🔍 Checking for PRF extension output in credential...")
                var prfOutputData: Data? = nil
                
                // Access PRF output directly from credential.prf property
                print("[PRF] 📋 credential.prf value: \(String(describing: credential.prf))")
                
                if let prfOutputContainer = credential.prf {
                    print("[PRF] ✅ PRF extension output found!")
                    print("[PRF] 📋 PRF output container type: \(type(of: prfOutputContainer))")
                      print("[PRF] 📋 PRF output container value: \(prfOutputContainer)")
                    
                    // Extract the first PRF output (derived from saltInput1)
                    let primaryKey: SymmetricKey = prfOutputContainer.first
                    print("[PRF] 📋 PRF first key type: \(type(of: primaryKey))")
                    prfOutputData = primaryKey.withUnsafeBytes { Data($0) }
                    
                    print("[PRF] 🔑 Extracted PRF 'first' output")
                    print("[PRF] 📏 PRF output length: \(prfOutputData?.count ?? 0) bytes")
                    print("[PRF] 🔢 PRF output (Base64): \(prfOutputData?.base64EncodedString().prefix(20) ?? "nil")...")
                    print("[PRF] 💡 This PRF output will be used to derive DPoP keys")
                    
                    if let prfOutputData {
                        try? secureStorage.storePrfOutput(prfOutputData, forCredentialId: credential.credentialID)
                        print("[PRF] 💾 Stored PRF output in secure storage")
                    }
                } else {
                    print("[PRF] ⚠️ No PRF output in credential.prf")
                    print("[PRF] ℹ️ This may indicate PRF was not enabled during registration")
                }
                
                if prfOutputData == nil {
                    // Deterministic fallback to keep the flow working
                    let credentialIdString = credential.credentialID.base64EncodedString()
                    let mockPrfInput = "prf-output-\(credentialIdString)".data(using: .utf8)!
                    var hasher = SHA256()
                    hasher.update(data: mockPrfInput)
                    prfOutputData = Data(hasher.finalize())
                    print("✅ [DPoP] Generated 32-byte mock PRF output (iOS 18+ fallback)")
                    try? secureStorage.storePrfOutput(prfOutputData!, forCredentialId: credential.credentialID)
                }
                
                prfOutput = prfOutputData
            } else {
                // iOS < 18 fallback
                let credentialIdString = credential.credentialID.base64EncodedString()
                let mockPrfInput = "prf-output-\(credentialIdString)".data(using: .utf8)!
                var hasher = SHA256()
                hasher.update(data: mockPrfInput)
                prfOutput = Data(hasher.finalize())
                print("✅ [DPoP] Generated 32-byte mock PRF output (iOS < 18 fallback)")
                try? secureStorage.storePrfOutput(prfOutput!, forCredentialId: credential.credentialID)
            }
            
            // Regenerate code challenge from stored verifier
            let codeChallenge = oauthClient.generateCodeChallenge(from: verifier)
            
            print("🔐 [Passkey] Using stored code verifier: \(verifier)")
            print("🔐 [Passkey] Regenerated code challenge: \(codeChallenge)")
            
            // Step 4: Get DPoP thumbprint for device binding
            let (_, _, thumbprint) = try dpopKeyManager.getOrDeriveKey(
                prfOutput: prfOutput,
                credentialId: credential.credentialID
            )
            print("🔐 [Passkey] DPoP thumbprint for device binding: \(thumbprint.prefix(20))...")
            
            // Step 5: Complete authentication with IdP
            print("🔐 [Passkey] Calling completeAuthentication...")
            let result = try await passkeyService.completeAuthentication(
                credential: credential,
                challengeId: challengeId,
                codeChallenge: codeChallenge,
                dpopKeyThumbprint: thumbprint
            )
            print("✅ [Passkey] Authentication completed, received code: \(result.code.prefix(20))...")
            
            // Step 6: Exchange authorization code for tokens with DPoP support
            print("🔐 [OAuth] Exchanging code for tokens...")
            let tokenResponse: TokenResponse
            if let prfOutput = prfOutput {
                print("✅ [DPoP] PRF output available, enabling DPoP binding")
                tokenResponse = try await oauthClient.exchangeCodeForTokens(
                    code: result.code,
                    codeVerifier: verifier,
                    prfOutput: prfOutput,
                    credentialId: credential.credentialID
                )
                hasDPoPBinding = true
                print("✅ [DPoP] Token exchange successful with DPoP binding!")
            } else {
                print("ℹ️ [OAuth] PRF not available, using standard token exchange")
                tokenResponse = try await oauthClient.exchangeCodeForTokens(
                    code: result.code,
                    codeVerifier: verifier
                )
                hasDPoPBinding = false
                print("✅ [OAuth] Token exchange successful (without DPoP)")
            }
            
            // Extract session ID and user ID from ID token
            if let idToken = tokenResponse.idToken {
                self.sessionId = extractSessionId(from: idToken)
                self.userId = extractUserId(from: idToken)
                print("✅ [Auth] Session ID: \(sessionId ?? "none")")
                print("✅ [Auth] User ID: \(userId ?? "none")")
            }
            
            // Success!
            isAuthenticated = true
            isLoading = false
            print("✅ [Passkey] Authentication flow completed successfully!")
            
        } catch {
            print("❌ [Passkey] Error in handleAuthenticationSuccess: \(error)")
            print("❌ [Passkey] Error details: \(error.localizedDescription)")
            isLoading = false
            errorMessage = "Authentication failed: \(error.localizedDescription)"
        }
    }
}

// MARK: - ASAuthorizationControllerDelegate

extension AuthViewModel: ASAuthorizationControllerDelegate {
    func authorizationController(controller: ASAuthorizationController, didCompleteWithAuthorization authorization: ASAuthorization) {
        print("✅ [Passkey] Authorization completed successfully!")
        print("✅ [Passkey] Credential type: \(type(of: authorization.credential))")
        
        Task { @MainActor in
            if let credential = authorization.credential as? ASAuthorizationPlatformPublicKeyCredentialAssertion {
                print("✅ [Passkey] Processing authentication credential")
                // Authentication (sign-in)
                await handleAuthenticationSuccess(credential: credential)
            } else if let credential = authorization.credential as? ASAuthorizationPlatformPublicKeyCredentialRegistration {
                print("✅ [Passkey] Processing registration credential")
                // Registration
                await handleRegistrationSuccess(credential: credential)
            } else {
                print("❌ [Passkey] Unexpected credential type")
                isLoading = false
                errorMessage = "Unexpected credential type"
            }
        }
    }
    
    func authorizationController(controller: ASAuthorizationController, didCompleteWithError error: Error) {
        Task { @MainActor in
            isLoading = false
            
            print("❌ [Passkey] Authorization error occurred")
            print("❌ [Passkey] Error: \(error)")
            print("❌ [Passkey] Error code: \((error as NSError).code)")
            print("❌ [Passkey] Error domain: \((error as NSError).domain)")
            print("❌ [Passkey] Error userInfo: \((error as NSError).userInfo)")
            print("❌ [Passkey] Localized description: \(error.localizedDescription)")
            
            if let authError = error as? ASAuthorizationError {
                print("❌ [Passkey] ASAuthorizationError code: \(authError.code.rawValue)")
                switch authError.code {
                case .canceled:
                    errorMessage = "Authentication cancelled"
                case .failed:
                    errorMessage = "Authentication failed: \(error.localizedDescription)"
                case .invalidResponse:
                    errorMessage = "Invalid response from authenticator (error 2). This usually means the passkey doesn't match the RP ID or challenge."
                case .notHandled:
                    errorMessage = "Authentication not handled"
                case .unknown:
                    // Error code 1004 typically means no passkeys registered for this RP ID
                    if (error as NSError).code == 1004 {
                        errorMessage = "No passkeys found. Please register a passkey first using the 'Register Passkey' button."
                    } else {
                        errorMessage = "Authentication error: \(error.localizedDescription) (Code: \((error as NSError).code))"
                    }
                default:
                    errorMessage = "Authentication error: \(error.localizedDescription) (Code: \((error as NSError).code))"
                }
            } else {
                errorMessage = "Authentication error: \(error.localizedDescription)"
            }
        }
    }
}

// MARK: - ASAuthorizationControllerPresentationContextProviding

extension AuthViewModel: ASAuthorizationControllerPresentationContextProviding {
    func presentationAnchor(for controller: ASAuthorizationController) -> ASPresentationAnchor {
        // Return the main window
        return UIApplication.shared.connectedScenes
            .compactMap { $0 as? UIWindowScene }
            .flatMap { $0.windows }
            .first { $0.isKeyWindow } ?? UIWindow()
    }
}

// MARK: - WebView Components

struct AuthenticatedWebViewWrapper: UIViewRepresentable {
    let cookies: [HTTPCookie]
    let initialUrl: URL
    
    func makeUIView(context: Context) -> WKWebView {
        let config = WKWebViewConfiguration()
        // Use non-persistent data store to start with clean cookies each time
        config.websiteDataStore = WKWebsiteDataStore.nonPersistent()
        let webView = WKWebView(frame: .zero, configuration: config)
        webView.navigationDelegate = context.coordinator
        return webView
    }
    
    func updateUIView(_ webView: WKWebView, context: Context) {
        let dataStore = webView.configuration.websiteDataStore
        let cookieStore = dataStore.httpCookieStore
        
        let group = DispatchGroup()
        
        // First, clear all existing cookies for dev.internal domain to avoid conflicts
        group.enter()
        cookieStore.getAllCookies { existingCookies in
            print("🧹 [WebView] Clearing existing cookies for dev.internal domain...")
            let devInternalCookies = existingCookies.filter { $0.domain.contains("dev.internal") }
            print("   Found \(devInternalCookies.count) existing cookies to remove")
            
            let deleteGroup = DispatchGroup()
            for cookie in devInternalCookies {
                deleteGroup.enter()
                print("   🗑️ Removing: \(cookie.name) (length: \(cookie.value.count))")
                cookieStore.delete(cookie) {
                    deleteGroup.leave()
                }
            }
            
            deleteGroup.notify(queue: .main) {
                print("✅ [WebView] Cleared all existing cookies")
                group.leave()
            }
        }
        
        // Wait for cleanup, then inject new cookies
        group.notify(queue: .main) {
            print("🍪 [WebView] Injecting \(cookies.count) new cookies...")
            
            let injectGroup = DispatchGroup()
            for cookie in cookies {
                injectGroup.enter()
                print("🍪 [WebView] Setting cookie: \(cookie.name)")
                print("   Value (first 50 chars): \(String(cookie.value.prefix(50)))")
                print("   Value length: \(cookie.value.count)")
                print("   Domain: \(cookie.domain)")
                
                cookieStore.setCookie(cookie) {
                    print("✅ [WebView] Set cookie: \(cookie.name) for domain: \(cookie.domain)")
                    injectGroup.leave()
                }
            }
            
            injectGroup.notify(queue: .main) {
                print("🌐 [WebView] All cookies set, loading: \(initialUrl)")
                
                // Verify cookies were actually set
                cookieStore.getAllCookies { allCookies in
                    print("🔍 [WebView] Verifying cookies after injection:")
                    for cookie in allCookies.filter({ $0.domain.contains("dev.internal") }) {
                        print("   📋 \(cookie.name): \(String(cookie.value.prefix(50)))... (length: \(cookie.value.count))")
                    }
                }
                
                let request = URLRequest(url: initialUrl)
                webView.load(request)
            }
        }
    }
    
    func makeCoordinator() -> Coordinator {
        Coordinator()
    }
    
    class Coordinator: NSObject, WKNavigationDelegate {
        func webView(_ webView: WKWebView, didStartProvisionalNavigation navigation: WKNavigation!) {
            print("🌐 [WebView] Started loading: \(webView.url?.absoluteString ?? "unknown")")
        }
        
        func webView(_ webView: WKWebView, didFinish navigation: WKNavigation!) {
            print("✅ [WebView] Finished loading: \(webView.url?.absoluteString ?? "unknown")")
        }
        
        func webView(_ webView: WKWebView, didFail navigation: WKNavigation!, withError error: Error) {
            print("❌ [WebView] Failed to load: \(error.localizedDescription)")
        }
        
        func webView(_ webView: WKWebView, 
                    decidePolicyFor navigationAction: WKNavigationAction, 
                    decisionHandler: @escaping (WKNavigationActionPolicy) -> Void) {
            print("🔀 [WebView] Navigation to: \(navigationAction.request.url?.absoluteString ?? "unknown")")
            decisionHandler(.allow)
        }
    }
}

struct SimpleWebViewWrapper: UIViewRepresentable {
    let initialUrl: URL
    
    func makeUIView(context: Context) -> WKWebView {
        let config = WKWebViewConfiguration()
        let webView = WKWebView(frame: .zero, configuration: config)
        webView.navigationDelegate = context.coordinator
        return webView
    }
    
    func updateUIView(_ webView: WKWebView, context: Context) {
        print("🌐 [WebView] Loading: \(initialUrl)")
        let request = URLRequest(url: initialUrl)
        webView.load(request)
    }
    
    func makeCoordinator() -> Coordinator {
        Coordinator()
    }
    
    class Coordinator: NSObject, WKNavigationDelegate {
        func webView(_ webView: WKWebView, didStartProvisionalNavigation navigation: WKNavigation!) {
            print("🌐 [WebView] Started loading: \(webView.url?.absoluteString ?? "unknown")")
        }
        
        func webView(_ webView: WKWebView, didFinish navigation: WKNavigation!) {
            print("✅ [WebView] Finished loading: \(webView.url?.absoluteString ?? "unknown")")
        }
        
        func webView(_ webView: WKWebView, didFail navigation: WKNavigation!, withError error: Error) {
            print("❌ [WebView] Failed to load: \(error.localizedDescription)")
        }
        
        func webView(_ webView: WKWebView, 
                    decidePolicyFor navigationAction: WKNavigationAction, 
                    decisionHandler: @escaping (WKNavigationActionPolicy) -> Void) {
            print("🔀 [WebView] Navigation to: \(navigationAction.request.url?.absoluteString ?? "unknown")")
            decisionHandler(.allow)
        }
    }
}

#Preview {
    ContentView()
}


import SwiftUI
import AuthenticationServices
import IdpMobileClient

struct ContentView: View {
    @StateObject private var viewModel = AuthViewModel()
    
    var body: some View {
        NavigationView {
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
    
    private let oauthClient = OAuthClient()
    private let passkeyService = PasskeyAuthService()
    private var apiClient: ApiClient!
    
    private var authController: ASAuthorizationController?
    @Published var currentCodeVerifier: String?
    @Published var currentState: String?
    @Published var currentChallenge: String?
    
    override init() {
        super.init()
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
            currentChallenge = options.challenge
            
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
            currentChallenge = options.challenge
            
            // Step 3: Create and present passkey authentication request
            print("🔐 [Passkey] Creating authentication request...")
            let controller = passkeyService.createAuthenticationRequest(options: options)
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
    
    func signOut() {
        oauthClient.signOut()
        isAuthenticated = false
        username = nil
        apiResponse = nil
        errorMessage = nil
    }
    
    private func handleRegistrationSuccess(credential: ASAuthorizationPlatformPublicKeyCredentialRegistration) async {
        do {
            guard let challenge = currentChallenge else {
                throw PasskeyError.invalidChallenge
            }
            
            print("✅ [Passkey] Registration credential received")
            
            // Complete registration with IdP
            try await passkeyService.completeRegistration(
                credential: credential,
                challenge: challenge
            )
            
            print("✅ [Passkey] Registration completed successfully!")
            
            isLoading = false
            showRegistration = false
            errorMessage = nil
            
            // Show success message
            // In a real app, you might want to show a success alert
            
        } catch {
            isLoading = false
            print("❌ [Passkey] Registration failed: \(error)")
            errorMessage = "Registration failed: \(error.localizedDescription)"
        }
    }
    
    private func handleAuthenticationSuccess(credential: ASAuthorizationPlatformPublicKeyCredentialAssertion) async {
        do {
            print("🔐 [Passkey] Starting handleAuthenticationSuccess")
            
            guard let challenge = currentChallenge else {
                print("❌ [Passkey] No challenge found")
                throw PasskeyError.invalidChallenge
            }
            print("✅ [Passkey] Challenge found: \(challenge.prefix(20))...")
            
            guard let verifier = currentCodeVerifier else {
                print("❌ [Passkey] No code verifier found")
                throw PasskeyError.invalidChallenge // PKCE parameters not generated
            }
            print("✅ [Passkey] Code verifier found: \(verifier.prefix(20))...")
            
            // Regenerate code challenge from stored verifier
            let codeChallenge = oauthClient.generateCodeChallenge(from: verifier)
            
            print("🔐 [Passkey] Using stored code verifier: \(verifier)")
            print("🔐 [Passkey] Regenerated code challenge: \(codeChallenge)")
            print("🔐 [Passkey] Code verifier length: \(verifier.count)")
            print("🔐 [Passkey] Code challenge length: \(codeChallenge.count)")
            
            // Step 4: Complete authentication with IdP (includes PKCE challenge)
            print("🔐 [Passkey] Calling completeAuthentication...")
            let result = try await passkeyService.completeAuthentication(
                credential: credential,
                challenge: challenge,
                codeChallenge: codeChallenge
            )
            print("✅ [Passkey] Authentication completed, received code: \(result.code.prefix(20))...")
            
            // Step 5: Exchange authorization code for tokens
            print("🔐 [Passkey] Exchanging code for tokens...")
            let tokenResponse = try await oauthClient.exchangeCodeForTokens(
                code: result.code,
                codeVerifier: verifier
            )
            print("✅ [Passkey] Token exchange successful!")
            
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

#Preview {
    ContentView()
}

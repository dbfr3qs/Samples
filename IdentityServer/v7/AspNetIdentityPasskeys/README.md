# Passkey Authentication with DPoP and PRF Extension

This example demonstrates a complete implementation of **passwordless authentication** using **WebAuthn/Passkeys** with advanced security features including **DPoP (Demonstrating Proof-of-Possession)** token binding and the **PRF (Pseudo-Random Function)** extension for device binding. The idea is to demonstrate a theoretical way to silently authenticate webviews in a mobile application.

## 🎯 What This Example Demonstrates

### Core Features

1. **Native iOS Passkey Authentication**
   - Native passkey registration and authentication using iOS AuthenticationServices framework
   - Biometric authentication (Face ID/Touch ID)
   - Secure credential storage in iOS Secure Enclave

2. **DPoP Token Binding**
   - OAuth 2.0 tokens bound to cryptographic keys
   - Prevents token theft and replay attacks
   - Enables secure token refresh without re-authentication

3. **PRF Extension for Device Binding**
   - Derives deterministic cryptographic keys from passkey authentication
   - Creates device-specific DPoP keys that don't sync across devices
   - Enables server-side device binding verification

4. **Mobile Session Exchange**
   - Secure cookie exchange for WebView authentication
   - Server-side session management with device binding
   - Seamless SSO between native app and embedded WebView

5. **PKCE (Proof Key for Code Exchange)**
   - Secure authorization code flow for mobile apps
   - Protection against authorization code interception

## 🏗️ Architecture

### Components

```
┌─────────────────────────────────────────────────────────────┐
│                     iOS Mobile App                          │
│  ┌──────────────┐  ┌──────────────┐  ┌──────────────┐       | 
│  │   Native     │  │   WebView    │  │  API Client  │       │
│  │   Passkey    │  │   (SSO)      │  │   (DPoP)     │       │
│  └──────────────┘  └──────────────┘  └──────────────┘       │
└─────────────────────────────────────────────────────────────┘
                            │
                            ▼
┌─────────────────────────────────────────────────────────────┐
│                    IdentityServer                           │
│  ┌──────────────┐  ┌──────────────┐  ┌──────────────┐       │
│  │   Passkey    │  │   Session    │  │    DPoP      │       │
│  │  Endpoints   │  │   Exchange   │  │  Validator   │       │
│  └──────────────┘  └──────────────┘  └──────────────┘       │
└─────────────────────────────────────────────────────────────┘
                            │
                            ▼
┌─────────────────────────────────────────────────────────────┐
│                   Protected Resources                       │
│  ┌──────────────┐  ┌──────────────┐                         │
│  │   Web App    │  │     API      │                         │
│  │  (WebView)   │  │   (DPoP)     │                         │
│  └──────────────┘  └──────────────┘                         │
└─────────────────────────────────────────────────────────────┘
```

### End-to-End Authentication Flow

This sequence diagram shows the complete flow from user sign-in to authenticated WebView loading:

```
┌─────────┐          ┌──────────────┐          ┌─────────────────┐          ┌─────────┐
│  User   │          │  Mobile App  │          │ IdentityServer  │          │ WebView │
└────┬────┘          └──────┬───────┘          └────────┬────────┘          └────┬────┘
     │                      │                           │                        │
     │ 1. Tap "Sign In"     │                           │                        │
     │─────────────────────>│                           │                        │
     │                      │                           │                        │
     │                      │ 2. Generate PKCE params   │                        │
     │                      │    (code_verifier,        │                        │
     │                      │     code_challenge)       │                        │
     │                      │                           │                        │
     │                      │ 3. Request authentication │                        │
     │                      │    challenge              │                        │
     │                      │──────────────────────────>│                        │
     │                      │                           │                        │
     │                      │ 4. Return challenge +     │                        │
     │                      │    challengeId            │                        │
     │                      │<──────────────────────────│                        │
     │                      │                           │                        │
     │                      │ 5. Generate PRF salt      │                        │
     │                      │    SHA256(rpId)           │                        │
     │                      │                           │                        │
     │ 6. Show Face ID      │                           │                        │
     │<─────────────────────│                           │                        │
     │                      │                           │                        │
     │ 7. Authenticate      │                           │                        │
     │─────────────────────>│                           │                        │
     │                      │                           │                        │
     │                      │ 8. Secure Enclave:        │                        │
     │                      │    - Signs challenge      │                        │
     │                      │    - PRF(device_key,      │                        │
     │                      │      salt)                │                        │
     │                      │    → 32-byte PRF output   │                        │
     │                      │                           │                        │
     │                      │ 9. Derive DPoP key:       │                        │
     │                      │    HKDF(PRF_output) →     │                        │
     │                      │    P-256 private key      │                        │
     │                      │                           │                        │
     │                      │ 10. Complete              │                        │
     │                      │     authentication        │                        │
     │                      │     + signed assertion    │                        │
     │                      │     + code_challenge      │                        │
     │                      │     + DPoP thumbprint     │                        │
     │                      │──────────────────────────>│                        │
     │                      │                           │                        │
     │                      │                           │ 11. Validate           │
     │                      │                           │     assertion &        │
     │                      │                           │     signature          │
     │                      │                           │                        │
     │                      │                           │ 12. Create mobile      │
     │                      │                           │     session with:      │
     │                      │                           │     - SessionId        │
     │                      │                           │     - SubjectId        │
     │                      │                           │     - DPoP thumbprint  │
     │                      │                           │                        │
     │                      │ 13. Return authorization  │                        │
     │                      │     code (linked to       │                        │
     │                      │     session)              │                        │
     │                      │<──────────────────────────│                        │
     │                      │                           │                        │
     │                      │ 14. Generate DPoP proof   │                        │
     │                      │     (JWT signed by DPoP   │                        │
     │                      │     private key)          │                        │
     │                      │                           │                        │
     │                      │ 15. Exchange code for     │                        │
     │                      │     tokens:               │                        │
     │                      │     + authorization_code  │                        │
     │                      │     + code_verifier       │                        │
     │                      │     + DPoP proof          │                        │
     │                      │──────────────────────────>│                        │
     │                      │                           │                        │
     │                      │                           │ 16. Validate:          │
     │                      │                           │     - code_verifier    │
     │                      │                           │     - DPoP proof       │
     │                      │                           │                        │
     │                      │ 17. Return DPoP-bound     │                        │
     │                      │     tokens:               │                        │
     │                      │     - ID token (with sid) │                        │
     │                      │     - Access token        │                        │
     │                      │     - Refresh token       │                        │
     │                      │<──────────────────────────│                        │
     │                      │                           │                        │
     │                      │ 18. Extract SessionId     │                        │
     │                      │     from ID token         │                        │
     │                      │                           │                        │
     │ 19. Tap "Open        │                           │                        │
     │     WebView"         │                           │                        │
     │─────────────────────>│                           │                        │
     │                      │                           │                        │
     │                      │ 20. Generate session      │                        │
     │                      │     assertion JWT:        │                        │
     │                      │     {                     │                        │
     │                      │       sid: "session_id",  │                        │
     │                      │       sub: "user_id",     │                        │
     │                      │       aud: "idp_url",     │                        │
     │                      │       jti: "unique_id"    │                        │
     │                      │     }                     │                        │
     │                      │     Signed with DPoP key  │                        │
     │                      │                           │                        │
     │                      │ 21. Request session       │                        │
     │                      │     exchange:             │                        │
     │                      │     + session assertion   │                        │
     │                      │     + DPoP proof          │                        │
     │                      │──────────────────────────>│                        │
     │                      │                           │                        │
     │                      │                           │ 22. Validate:          │
     │                      │                           │     - DPoP proof       │
     │                      │                           │     - Session exists   │
     │                      │                           │     - Thumbprint       │
     │                      │                           │       matches stored   │
     │                      │                           │                        │
     │                      │                           │ 23. Create auth        │
     │                      │                           │     cookie with        │
     │                      │                           │     required claims    │
     │                      │                           │                        │
     │                      │ 24. Return authentication │                        │
     │                      │     cookie                │                        │
     │                      │<──────────────────────────│                        │
     │                      │                           │                        │
     │                      │ 25. Clear old cookies     │                        │
     │                      │                           │                        │
     │                      │ 26. Inject cookie into    │                        │
     │                      │     WebView               │                        │
     │                      │──────────────────────────────────────────────────>│
     │                      │                           │                        │
     │                      │                           │                        │ 27. Load web app
     │                      │                           │                        │     (web.dev.
     │                      │                           │                        │     internal)
     │                      │                           │                        │
     │                      │                           │ 28. Web app initiates  │
     │                      │                           │     OIDC flow          │
     │                      │                           │<───────────────────────│
     │                      │                           │                        │
     │                      │                           │ 29. Recognize          │
     │                      │                           │     authenticated      │
     │                      │                           │     user from cookie   │
     │                      │                           │                        │
     │                      │                           │ 30. Complete OIDC      │
     │                      │                           │     flow (no login     │
     │                      │                           │     prompt)            │
     │                      │                           │──────────────────────>│
     │                      │                           │                        │
     │                      │                           │                        │ 31. Display
     │                      │                           │                        │     authenticated
     │ 32. See authenticated│                           │                        │     content
     │     WebView content  │                           │                        │
     │<─────────────────────┼───────────────────────────┼────────────────────────│
     │                      │                           │                        │
```

### Key Points

- **Steps 1-17**: Native passkey authentication with PKCE and DPoP
- **Steps 18-24**: Session exchange for WebView SSO
- **Steps 25-31**: WebView loads with injected cookie, completes OIDC flow silently
- **Device Binding**: DPoP thumbprint stored in step 12, verified in step 22
- **No Login Prompt**: WebView user sees no authentication UI (step 32)

## 🔐 Security Features

### 1. Device Binding via PRF

**Problem**: Passkeys sync across devices via iCloud Keychain, but you want to bind sessions to specific devices.

**Solution**: The PRF extension generates device-specific cryptographic material:

```
Device A (iPhone):
  Passkey → PRF(device_key_A, salt) → DPoP Key A → Thumbprint A

Device B (iPad, same passkey synced):
  Passkey → PRF(device_key_B, salt) → DPoP Key B → Thumbprint B
```

The server stores `Thumbprint A` during authentication and verifies it during session exchange. If someone steals the session ID and tries to use it from Device B, the server rejects it because `Thumbprint B ≠ Thumbprint A`.

### 2. DPoP Token Binding

**Problem**: OAuth access tokens can be stolen and replayed by attackers.

**Solution**: DPoP binds tokens to cryptographic keys:

```
Token Request:
  POST /connect/token
  DPoP: <JWT signed by private key>
  
Token Response:
  {
    "access_token": "...",
    "token_type": "DPoP"  // Token is bound to DPoP key
  }

API Request:
  GET /api/resource
  Authorization: DPoP <access_token>
  DPoP: <JWT signed by same private key>
```

The API validates that:
1. The DPoP proof is signed by the same key that was bound to the token
2. The proof is fresh (timestamp validation)
3. The proof is for this specific request (HTTP method + URL)

### 3. PKCE Protection

**Problem**: Authorization codes can be intercepted on mobile devices.

**Solution**: PKCE ensures only the app that initiated the flow can exchange the code:

```
1. App generates random code_verifier
2. App sends SHA256(code_verifier) as code_challenge
3. Server stores code_challenge with authorization code
4. App exchanges code + code_verifier
5. Server verifies SHA256(code_verifier) == code_challenge
```

### 4. Replay Protection

Multiple layers prevent replay attacks:
- **DPoP JTI**: Each proof has a unique ID, server caches used JTIs
- **Timestamps**: Proofs expire after a short time window
- **Passkey Counter**: WebAuthn signature counter prevents credential replay

## 🚀 Getting Started

See [SETUP.md](SETUP.md) for detailed setup instructions.

### Quick Start

```bash
# 1. Start IdentityServer
cd IdentityServerAspNetIdentityPasskeys
dotnet run

# 2. Start API
cd Api
dotnet run

# 3. Start WebView App
cd WebViewApp
dotnet run

# 4. Build and run iOS app
cd IdpMobileClient
./setup-xcode.sh
# Open in Xcode and run
```

## 📱 iOS App Features

### Native Passkey Authentication
- Register passkeys with biometric authentication
- Sign in without passwords
- Secure credential storage in Secure Enclave

### DPoP-Protected API Calls
- All API requests include DPoP proofs
- Tokens are cryptographically bound to the device
- Automatic token refresh without re-authentication

### WebView SSO
- Embedded WebView loads authenticated content
- No login prompt (cookies injected via session exchange)
- Full OIDC flow with device-bound session

## 🔧 Technical Details

### PRF Salt Generation

```swift
let rpId = "idp.dev.internal"
let prfSalt = SHA256.hash(data: Data(rpId.utf8))
```

The salt is deterministic (always the same for a given RP ID), ensuring the PRF output is consistent across authentications on the same device.

### DPoP Key Derivation

```swift
let seed = HKDF<SHA256>.deriveKey(
    inputKeyMaterial: SymmetricKey(data: prfOutput),
    salt: Data(),
    info: Data("webauthn-prf:dpop:v1".utf8) + Data(rpId.utf8) + credentialId,
    outputByteCount: 32
)
let privateKey = try P256.Signing.PrivateKey(x963Representation: seed)
```

The DPoP key is derived using HKDF with:
- **Input**: PRF output (device-specific)
- **Info**: Domain separation string + RP ID + credential ID
- **Output**: P-256 private key for signing DPoP proofs

### Session Exchange Flow

```
1. Mobile app generates session assertion:
   {
     "sid": "session_id_from_id_token",
     "sub": "user_id",
     "aud": "https://idp.dev.internal",
     "jti": "unique_id",
     "exp": timestamp
   }
   Signed with DPoP key

2. Server validates:
   - DPoP proof signature
   - Session exists and belongs to user
   - DPoP thumbprint matches stored binding
   - JTI not replayed

3. Server returns:
   - ASP.NET Identity authentication cookie
   - Cookie includes required IdentityServer claims
   - Cookie domain set to idp.dev.internal

4. Mobile app injects cookie into WebView
   - WebView loads web app
   - Web app initiates OIDC flow
   - IdentityServer recognizes authenticated user
   - Completes flow without login prompt
```

## 📊 Key Differences from Standard OAuth

| Feature | Standard OAuth | This Implementation |
|---------|---------------|---------------------|
| Authentication | Password/Social | Passkey (biometric) |
| Token Binding | Bearer tokens | DPoP-bound tokens |
| Device Binding | None | PRF-based binding |
| Token Theft Protection | Minimal | Strong (DPoP + device binding) |
| Mobile Flow | Browser redirect | Native passkey prompt |
| WebView Auth | Separate login | SSO via session exchange |

## 🎓 Learning Outcomes

After studying this example, you'll understand:

1. How to implement WebAuthn/Passkeys in a mobile app
2. How to use the PRF extension for key derivation
3. How to implement DPoP for token binding
4. How to create device binding even with synced passkeys
5. How to implement secure session exchange for WebViews
6. How to protect against token theft and replay attacks
7. How to implement PKCE for mobile OAuth flows

## 📚 Standards Implemented

- [WebAuthn Level 3](https://www.w3.org/TR/webauthn-3/)
- [PRF Extension](https://w3c.github.io/webauthn/#prf-extension)
- [OAuth 2.0 DPoP](https://datatracker.ietf.org/doc/html/rfc9449)
- [PKCE (RFC 7636)](https://datatracker.ietf.org/doc/html/rfc7636)
- [OAuth 2.0 for Native Apps (RFC 8252)](https://datatracker.ietf.org/doc/html/rfc8252)

## ⚠️ Production Considerations

This is a **demonstration/reference implementation**. For production:

1. **Certificate Management**: Use valid SSL certificates from a trusted CA
2. **Key Storage**: Implement proper key backup and recovery mechanisms
3. **Error Handling**: Add comprehensive error handling and user feedback
4. **Logging**: Implement secure logging (avoid logging sensitive data)
5. **Rate Limiting**: Add rate limiting to prevent abuse
6. **Monitoring**: Monitor for suspicious authentication patterns
7. **User Management**: Add account recovery flows for lost devices
8. **Testing**: Add comprehensive unit and integration tests

## 📄 License

This example is provided as-is for educational purposes.

## 🤝 Contributing

This is a reference implementation. Feel free to use it as a starting point for your own projects.

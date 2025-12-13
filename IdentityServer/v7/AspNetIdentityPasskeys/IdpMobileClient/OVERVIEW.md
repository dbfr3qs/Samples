# IdpMobileClient - Complete Overview

## What This Is

A native iOS 18 Swift application that demonstrates secure authentication with IdentityServer using:
- **Passkeys** (WebAuthn) for passwordless authentication
- **PKCE OAuth2** authorization code flow
- **Refresh tokens** for long-lived sessions
- **Secure token storage** in iOS Keychain
- **Authenticated API calls** using Bearer tokens

## Architecture

```
┌─────────────────────────────────────────────────────────────┐
│                     iOS Mobile App                          │
│                                                             │
│  ┌──────────────┐  ┌──────────────┐  ┌──────────────┐    │
│  │   SwiftUI    │  │    OAuth     │  │   Passkey    │    │
│  │     View     │──│    Client    │──│   Service    │    │
│  └──────────────┘  └──────────────┘  └──────────────┘    │
│         │                  │                  │            │
│         │                  │                  │            │
│         └──────────────────┴──────────────────┘            │
│                            │                               │
│                    ┌───────▼────────┐                      │
│                    │  Token Storage │                      │
│                    │   (Keychain)   │                      │
│                    └────────────────┘                      │
└─────────────────────────────────────────────────────────────┘
                             │
                             │ HTTPS
                             │
        ┌────────────────────┴────────────────────┐
        │                                         │
        ▼                                         ▼
┌───────────────────┐                    ┌────────────────┐
│  IdentityServer   │                    │  Protected API │
│  (IdP)            │                    │                │
│                   │                    │                │
│  - OAuth/OIDC     │                    │  - Bearer Auth │
│  - WebAuthn       │                    │  - Resources   │
│  - Token Endpoint │                    │                │
└───────────────────┘                    └────────────────┘
```

## Authentication Flow

### 1. Passkey Sign-In

```
User                 iOS App              IdentityServer
  │                     │                        │
  │  Tap "Sign In"      │                        │
  ├────────────────────>│                        │
  │                     │  POST /authenticate/   │
  │                     │       begin            │
  │                     ├───────────────────────>│
  │                     │                        │
  │                     │  Challenge + Options   │
  │                     │<───────────────────────┤
  │                     │                        │
  │  Passkey Prompt     │                        │
  │<────────────────────┤                        │
  │                     │                        │
  │  Biometric Auth     │                        │
  ├────────────────────>│                        │
  │                     │                        │
  │                     │  POST /authenticate/   │
  │                     │       complete         │
  │                     │  + Assertion           │
  │                     ├───────────────────────>│
  │                     │                        │
  │                     │  Authorization Code    │
  │                     │<───────────────────────┤
  │                     │                        │
```

### 2. Token Exchange (PKCE)

```
iOS App                                  IdentityServer
  │                                            │
  │  Generate code_verifier                   │
  │  Generate code_challenge (SHA256)         │
  │                                            │
  │  POST /connect/token                      │
  │  - grant_type: authorization_code         │
  │  - code: [from step 1]                    │
  │  - code_verifier: [generated]             │
  │  - client_id: mobile-client               │
  │  - redirect_uri: com.idp.mobile://callback│
  ├───────────────────────────────────────────>│
  │                                            │
  │  Verify code_challenge matches            │
  │  code_verifier (PKCE validation)          │
  │                                            │
  │  Token Response:                          │
  │  - access_token                           │
  │  - refresh_token                          │
  │  - expires_in                             │
  │<───────────────────────────────────────────┤
  │                                            │
  │  Store tokens in Keychain                 │
  │                                            │
```

### 3. API Call with Token

```
iOS App                                  Protected API
  │                                            │
  │  Check token expiry                       │
  │  Token valid? ────No───> Refresh token    │
  │       │                                    │
  │      Yes                                   │
  │       │                                    │
  │  GET /api/resource                        │
  │  Authorization: Bearer [access_token]     │
  ├───────────────────────────────────────────>│
  │                                            │
  │  Validate token                           │
  │  Check scopes                             │
  │                                            │
  │  Resource Data                            │
  │<───────────────────────────────────────────┤
  │                                            │
```

### 4. Token Refresh

```
iOS App                                  IdentityServer
  │                                            │
  │  Access token expired                     │
  │                                            │
  │  POST /connect/token                      │
  │  - grant_type: refresh_token              │
  │  - refresh_token: [stored token]          │
  │  - client_id: mobile-client               │
  ├───────────────────────────────────────────>│
  │                                            │
  │  Validate refresh token                   │
  │                                            │
  │  New Token Response:                      │
  │  - access_token (new)                     │
  │  - refresh_token (new/same)               │
  │  - expires_in                             │
  │<───────────────────────────────────────────┤
  │                                            │
  │  Update tokens in Keychain                │
  │                                            │
```

## Key Components

### OAuthClient.swift
- Generates PKCE parameters (code_verifier, code_challenge)
- Builds authorization URLs
- Exchanges authorization codes for tokens
- Refreshes access tokens automatically
- Manages token lifecycle

### PasskeyAuthService.swift
- Interfaces with iOS AuthenticationServices framework
- Handles WebAuthn registration and authentication
- Communicates with IdP's passkey endpoints
- Manages passkey credentials

### TokenStorage.swift
- Securely stores tokens in iOS Keychain
- Provides token retrieval and validation
- Handles token expiry checking
- Clears tokens on sign-out

### ApiClient.swift
- Makes authenticated HTTP requests
- Automatically refreshes expired tokens
- Adds Bearer authorization headers
- Handles API errors

### ContentView.swift
- SwiftUI interface
- Sign-in button (triggers passkey flow)
- API call button (makes authenticated request)
- Displays authentication state
- Shows API responses

## Security Features

### 1. PKCE (Proof Key for Code Exchange)
- Prevents authorization code interception attacks
- Required for public clients (mobile apps)
- Code verifier never leaves the device until token exchange
- Code challenge sent in authorization request

### 2. Passkeys (WebAuthn)
- Passwordless authentication
- Biometric verification (Face ID/Touch ID)
- Private key never leaves device
- Phishing-resistant
- Syncs across user's Apple devices via iCloud Keychain

### 3. Secure Token Storage
- iOS Keychain for token persistence
- Hardware-backed encryption
- Protected by device passcode/biometrics
- Isolated per-app storage

### 4. Refresh Tokens
- Long-lived sessions without re-authentication
- Sliding expiration window
- Can be revoked by IdP
- Reduces password/passkey prompts

### 5. HTTPS/TLS
- All communication encrypted
- Certificate validation
- Protection against MITM attacks

## Configuration

### Default Settings

| Setting | Value |
|---------|-------|
| IdP URL | `https://idp.dev.internal:5000` |
| API URL | `https://api.dev.internal:5002` |
| Client ID | `mobile-client` |
| Redirect URI | `com.idp.mobile://callback` |
| Scopes | `openid profile email api offline_access` |
| Min iOS Version | 18.0 |

### Customization Points

All URLs and identifiers can be changed in the respective Swift files:
- `OAuthClient.swift` - OAuth configuration
- `PasskeyAuthService.swift` - WebAuthn/passkey settings
- `ApiClient.swift` - API endpoint
- `Info.plist` - URL schemes, ATS exceptions

## Development Setup

### Prerequisites
- macOS with Xcode 15+
- iOS 18 simulator or device
- IdentityServer with WebAuthn support
- Protected API endpoint

### Quick Start
```bash
cd IdpMobileClient
./setup-xcode.sh
```

### Manual Setup
1. Install XcodeGen: `brew install xcodegen`
2. Generate project: `xcodegen generate`
3. Open `IdpMobileClient.xcodeproj`
4. Select target and set Development Team
5. Build and run

## Testing

### Test Passkey Flow
1. Launch app on iOS 18 device/simulator
2. Tap "Sign in with Passkey"
3. Complete biometric authentication
4. Verify "Signed In" state

### Test API Call
1. After signing in, tap "Call API"
2. Verify API response displays
3. Check that Bearer token is sent

### Test Token Refresh
1. Wait for access token to expire (or modify expiry time)
2. Tap "Call API" again
3. Verify automatic token refresh
4. Verify API call succeeds with new token

### Test Sign Out
1. Tap "Sign Out"
2. Verify tokens cleared from Keychain
3. Verify return to sign-in screen

## Troubleshooting

### Common Issues

**Passkey prompt doesn't appear**
- Ensure iOS 15+ device/simulator
- Check relying party identifier matches IdP domain
- Verify associated domains configured

**Network errors**
- Check IdP and API URLs are correct
- Verify ATS exceptions in Info.plist
- Ensure device can reach internal domains

**Token refresh fails**
- Verify `offline_access` scope requested
- Check IdP returns refresh tokens
- Confirm refresh token not expired

**Build errors**
- Clean build folder (Cmd+Shift+K)
- Delete DerivedData
- Regenerate Xcode project

## Production Considerations

### Before Deploying

1. **Update URLs**: Replace `.dev.internal` with production domains
2. **SSL Certificates**: Use valid certificates (not self-signed)
3. **Associated Domains**: Configure with production Team ID
4. **Token Lifetimes**: Adjust based on security requirements
5. **Error Handling**: Add comprehensive error handling
6. **Logging**: Implement proper logging (avoid logging tokens)
7. **Certificate Pinning**: Consider for high-security apps
8. **Jailbreak Detection**: Consider for sensitive apps
9. **App Store**: Prepare privacy policy, screenshots, etc.

### Security Checklist

- [ ] PKCE enabled and enforced
- [ ] Client secret not embedded (public client)
- [ ] Tokens stored in Keychain only
- [ ] HTTPS enforced for all requests
- [ ] Certificate validation enabled
- [ ] Refresh tokens rotated
- [ ] Token expiry properly handled
- [ ] Sign-out clears all tokens
- [ ] No tokens in logs or analytics
- [ ] Associated domains configured
- [ ] URL scheme validated

## Further Reading

- [OAuth 2.0 for Native Apps (RFC 8252)](https://tools.ietf.org/html/rfc8252)
- [PKCE (RFC 7636)](https://tools.ietf.org/html/rfc7636)
- [WebAuthn Specification](https://www.w3.org/TR/webauthn/)
- [Apple AuthenticationServices Framework](https://developer.apple.com/documentation/authenticationservices)
- [IdentityServer Documentation](https://docs.duendesoftware.com/identityserver)

## Support

For issues or questions:
1. Check `SETUP.md` for setup instructions
2. Review `IDP_CONFIGURATION.md` for IdP configuration
3. See `TROUBLESHOOTING.md` for common problems
4. Check IdentityServer logs for authentication issues
5. Use Xcode debugger and console for app issues

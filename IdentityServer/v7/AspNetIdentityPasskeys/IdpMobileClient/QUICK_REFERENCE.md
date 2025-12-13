# Quick Reference

## Setup (30 seconds)

```bash
cd IdpMobileClient
./setup-xcode.sh
# Opens Xcode automatically
# Set your Development Team
# Build and run (⌘R)
```

## Key Files

| File | Purpose |
|------|---------|
| `OAuthClient.swift` | PKCE OAuth2 flow, token management |
| `PasskeyAuthService.swift` | WebAuthn/passkey authentication |
| `TokenStorage.swift` | Secure Keychain token storage |
| `ApiClient.swift` | Authenticated API requests |
| `ContentView.swift` | SwiftUI UI and view logic |

## Configuration

### Change IdP URL
```swift
// OAuthClient.swift, line 8
let idpBaseURL: String = "https://your-idp.com"

// PasskeyAuthService.swift, line 11
let idpBaseURL: String = "https://your-idp.com"
```

### Change API URL
```swift
// ApiClient.swift, line 8
let apiBaseURL: String = "https://your-api.com"
```

### Change Client ID
```swift
// OAuthClient.swift, line 9
let clientId: String = "your-client-id"
```

### Change Redirect URI
```swift
// OAuthClient.swift, line 10
let redirectUri: String = "your.app://callback"

// Also update Info.plist CFBundleURLSchemes
```

## Common Tasks

### Add a new API endpoint
```swift
// In ApiClient.swift
public func getUsers() async throws -> [User] {
    let data = try await get(path: "/users")
    return try JSONDecoder().decode([User].self, from: data)
}
```

### Change token scopes
```swift
// OAuthClient.swift, line 38
URLQueryItem(name: "scope", value: "openid profile email your-scope offline_access")
```

### Customize passkey prompt
```swift
// PasskeyAuthService.swift
// Modify createAuthenticationRequest() method
```

### Add custom UI
```swift
// ContentView.swift
// Modify the SwiftUI views
```

## Testing

### Test on Simulator
```bash
# iOS 18 simulator required
# Passkeys work in simulator
```

### Test on Device
```bash
# Requires:
# - Development Team set
# - Device registered
# - Provisioning profile
```

### Debug Network Calls
```swift
// Add to URLSession requests:
request.timeoutInterval = 30
print("Request: \(request.url?.absoluteString ?? "")")
```

## Troubleshooting

| Problem | Solution |
|---------|----------|
| Build fails | Clean build folder (⌘⇧K), delete DerivedData |
| Passkey doesn't work | Check iOS 15+, verify rpId matches domain |
| Network error | Check URLs, verify ATS exceptions in Info.plist |
| Token refresh fails | Verify `offline_access` scope, check IdP config |
| Can't open project | Run `./setup-xcode.sh` again |

## IdP Requirements

Your IdentityServer must have:

1. **OAuth Client** configured:
   - Client ID: `mobile-client`
   - Grant type: `authorization_code`
   - PKCE: `required`
   - Redirect URI: `com.idp.mobile://callback`
   - Scopes: `openid profile email api offline_access`

2. **WebAuthn Endpoints**:
   - `POST /api/passkey/authenticate/begin`
   - `POST /api/passkey/authenticate/complete`

3. **Token Endpoint**:
   - `POST /connect/token`

See `IDP_CONFIGURATION.md` for details.

## API Requirements

Your API must:

1. Accept Bearer tokens in Authorization header
2. Validate tokens with IdP
3. Return appropriate HTTP status codes
4. Support CORS if needed

## Useful Commands

```bash
# Generate Xcode project
xcodegen generate

# Open in Xcode
open IdpMobileClient.xcodeproj

# Clean build
rm -rf .build DerivedData

# View Keychain items (on device)
# Settings → Developer → Keychain

# View console logs
# Xcode → Window → Devices and Simulators → View Device Logs
```

## Code Snippets

### Get current access token
```swift
let token = try await oauthClient.getValidAccessToken()
```

### Make API call
```swift
let data = try await apiClient.get(path: "/endpoint")
```

### Check if signed in
```swift
let isSignedIn = tokenStorage.isAccessTokenValid()
```

### Sign out
```swift
oauthClient.signOut()
```

### Handle errors
```swift
do {
    try await apiClient.callTestEndpoint()
} catch ApiError.httpError(let statusCode) {
    print("HTTP error: \(statusCode)")
} catch {
    print("Error: \(error.localizedDescription)")
}
```

## Architecture at a Glance

```
User Taps Button
       ↓
  ContentView (SwiftUI)
       ↓
  AuthViewModel
       ↓
  ┌────────────────┐
  │ PasskeyService │ → IdP WebAuthn endpoints
  └────────────────┘
       ↓
  ┌────────────────┐
  │  OAuthClient   │ → IdP Token endpoint
  └────────────────┘
       ↓
  ┌────────────────┐
  │ TokenStorage   │ → iOS Keychain
  └────────────────┘
       ↓
  ┌────────────────┐
  │   ApiClient    │ → Protected API
  └────────────────┘
```

## Next Steps

1. ✅ Run `./setup-xcode.sh`
2. ✅ Open project in Xcode
3. ✅ Set Development Team
4. ✅ Configure IdP (see `IDP_CONFIGURATION.md`)
5. ✅ Build and run
6. ✅ Test sign-in flow
7. ✅ Test API call
8. ✅ Customize for your needs

## Resources

- Full docs: `OVERVIEW.md`
- Setup guide: `SETUP.md`
- IdP config: `IDP_CONFIGURATION.md`
- README: `README.md`

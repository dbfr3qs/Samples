# DPoP-Based Mobile Sessions Implementation

## Overview

This implementation extends the native passkey authentication system with **DPoP (Demonstrating Proof-of-Possession)** bound refresh tokens using the **WebAuthn PRF (Pseudo-Random Function)** extension. It enables device-bound mobile sessions tracked server-side without relying on browser cookies, and includes WebView SSO capabilities.

## Key Features

✅ **RFC 9449 Compliant DPoP**: Stable keys with unique proofs (jti + iat per request)  
✅ **PRF-Based Key Derivation**: Leverages passkey PRF extension for deterministic key generation  
✅ **No Passkey Prompts on Refresh**: Cached PRF output (15-day expiry) enables seamless token refresh  
✅ **Server-Side Session Tracking**: Sessions updated on each token refresh with device information  
✅ **Strong Device Binding**: Refresh tokens cryptographically bound to device via DPoP  
✅ **WebView SSO**: Seamless authentication sharing between native and web contexts  

## Architecture

### DPoP Flow

```
1. Initial Authentication:
   User → Passkey Auth (with PRF) → PRF Output (32 bytes)
   PRF Output → HKDF → P-256 Private Key (stable, cached)
   Private Key → JWK → SHA-256 → Thumbprint

2. Token Exchange:
   Generate DPoP Proof (unique jti + iat, stable key)
   POST /connect/token + DPoP header
   Server validates proof, creates device binding
   Returns access_token + refresh_token (bound to DPoP key)

3. Token Refresh (no passkey prompt):
   Generate NEW DPoP Proof (new jti + iat, same cached key)
   POST /connect/token + DPoP header
   Server validates proof, checks device binding
   Updates server-side session
   Returns new access_token

4. API Access:
   Generate DPoP Proof (with ath = hash(access_token))
   GET /api/resource + Authorization: DPoP <token> + DPoP header
   API validates DPoP binding
```

## Implementation Components

### Server-Side (C# / .NET)

#### 1. Database Schema

**DeviceBinding Entity** (`Models/DeviceBinding.cs`):
- Stores DPoP public key JWK and thumbprint
- Tracks refresh count and last usage
- Links to user and credential

#### 2. Core Services

**PrfOutputStore** (`Services/PrfOutputStore.cs`):
- Temporarily stores PRF output during authentication flow
- In-memory cache with 5-minute expiry

**DeviceBindingStore** (`Services/DeviceBindingStore.cs`):
- CRUD operations for device bindings
- Query by thumbprint, refresh token, or user ID

**DPoPProofValidator** (`Services/DPoPProofValidator.cs`):
- RFC 9449 compliant validation
- Verifies JWT signature, claims, timestamp
- Validates HTTP method and URI matching
- Computes JWK thumbprint

**ReplayCache** (`Services/ReplayCache.cs`):
- Prevents DPoP proof reuse via jti tracking
- Distributed cache with 5-minute retention

**DPoPTokenRequestValidator** (`Services/DPoPTokenRequestValidator.cs`):
- Custom token request validator for IdentityServer
- Creates device binding on authorization code grant
- Validates device binding on refresh token grant
- Adds dpop_jkt claim to tokens

**DPoPSessionCoordinator** (`Services/DPoPSessionCoordinator.cs`):
- Implements ISessionCoordinationService
- Tracks DPoP device usage in server-side sessions

#### 3. Endpoints

**MobilePasskeyEndpoints** (`Passkeys/MobilePasskeyEndpoints.cs`):
- Enhanced with PRF extension support
- Generates user-specific PRF salt: `SHA256(userId)`
- Returns PRF extension in registration/authentication options
- Stores PRF salt with credential

### iOS Client (Swift)

#### 1. Secure Storage

**SecureStorage** (`SecureStorage.swift`):
- Stores PRF output in Keychain with 15-day expiry
- Stores derived DPoP keys in Keychain
- Uses `kSecAttrAccessibleAfterFirstUnlockThisDeviceOnly`

#### 2. Key Management

**DPoPKeyManager** (`DPoPKeyManager.swift`):
- Derives P-256 private key from PRF output using HKDF
- Generates JWK representation (public key only)
- Calculates RFC 7638 JWK thumbprint
- Caches derived keys for reuse

#### 3. Proof Generation

**DPoPProofGenerator** (`DPoPProofGenerator.swift`):
- Generates RFC 9449 compliant DPoP proofs
- Creates JWT with header: `{"typ":"dpop+jwt","alg":"ES256","jwk":{...}}`
- Payload includes: `jti` (UUID), `htm`, `htu`, `iat`
- Optional `ath` claim for API requests
- Signs with ES256 (ECDSA P-256 + SHA-256)
- Converts DER signature to raw format (r || s)

#### 4. OAuth Integration

**OAuthClient** (`OAuthClient.swift`):
- Enhanced with DPoP support
- Adds DPoP header to token exchange requests
- Adds DPoP header to refresh token requests
- Uses cached key for refresh (no passkey prompt)

#### 5. Passkey Service

**PasskeyAuthService** (`PasskeyAuthService.swift`):
- Enhanced with PRF extension support (iOS 17+)
- Adds PRF input to registration/authentication requests
- Extracts PRF output from credential responses

#### 6. WebView SSO

**AuthenticatedWebView** (`AuthenticatedWebView.swift`):
- Intercepts navigation to IdP authorize endpoint
- Injects `id_token_hint` and `prompt=none` parameters
- Enables seamless SSO for web content in mobile app

## Configuration

### Server Configuration

**HostingExtensions.cs**:
```csharp
// Register DPoP services
builder.Services.AddScoped<IPrfOutputStore, PrfOutputStore>();
builder.Services.AddScoped<DPoPProofValidator>();
builder.Services.AddScoped<IReplayCache, ReplayCache>();
builder.Services.AddScoped<IDeviceBindingStore, DeviceBindingStore>();
builder.Services.AddHttpContextAccessor();

// Register DPoP custom validators
builder.Services.AddTransient<ICustomTokenRequestValidator, DPoPTokenRequestValidator>();
builder.Services.AddTransient<ISessionCoordinationService, DPoPSessionCoordinator>();
```

### Database Migration

```bash
# Create migration
dotnet ef migrations add AddDeviceBinding --project IdentityServerAspNetIdentityPasskeys

# Apply migration
dotnet ef database update --project IdentityServerAspNetIdentityPasskeys
```

### iOS Configuration

**Requirements**:
- iOS 17.0+ for PRF extension support
- iOS 16.0+ for passkey authentication

**Usage Example**:
```swift
// Initialize services
let passkeyService = PasskeyAuthService()
let oauthClient = OAuthClient()
let secureStorage = SecureStorage()

// Registration with PRF
let options = try await passkeyService.beginRegistration(username: "user@example.com")
let controller = passkeyService.createRegistrationRequest(options: options)
// ... handle authorization controller delegate

// Authentication with PRF
let authOptions = try await passkeyService.beginAuthentication()
let authController = passkeyService.createAuthenticationRequest(
    options: authOptions,
    prfSalt: prfSalt // Retrieved from stored credential
)
// ... handle authorization controller delegate

// Extract PRF output (iOS 17+)
if #available(iOS 17.0, *) {
    if let prfOutput = credential.prf?.first {
        try secureStorage.storePrfOutput(prfOutput, forCredentialId: credential.credentialID)
    }
}

// Token exchange with DPoP
let tokenResponse = try await oauthClient.exchangeCodeForTokens(
    code: authCode,
    codeVerifier: codeVerifier,
    prfOutput: prfOutput,
    credentialId: credentialId
)

// Token refresh with DPoP (no passkey prompt)
let refreshResponse = try await oauthClient.refreshAccessToken(
    credentialId: credentialId
)
```

## Security Considerations

### PRF Salt Management
- User-specific salts derived from user ID: `SHA256(userId)`
- Deterministic generation ensures consistency
- Stored with credential for future authentications

### Key Storage
- PRF output cached in Keychain with 15-day expiry
- Derived keys stored in Keychain with device-only access
- Automatic cleanup on expiry

### Proof Expiration
- 60-second timestamp tolerance enforced
- Each proof must have unique `jti` (UUID)
- Proofs cannot be reused (replay protection)

### Replay Protection
- JTI values tracked in distributed cache
- 5-minute retention window
- Prevents proof reuse attacks

### Session Binding
- DPoP key thumbprint verified on every refresh
- Device binding checked against stored thumbprint
- Mismatched thumbprints rejected

### Revocation
- Device bindings can be deleted to revoke access
- Refresh tokens bound to deleted bindings become invalid
- Server-side session management UI can display/revoke devices

## Testing

### Server-Side Tests

1. **DPoP Proof Validation**:
   - Valid proof acceptance
   - Invalid signature rejection
   - Expired timestamp rejection
   - Replay attack prevention
   - HTTP method/URI mismatch rejection

2. **Device Binding**:
   - Creation on authorization code grant
   - Validation on refresh token grant
   - Thumbprint mismatch rejection
   - Multiple devices per user

3. **Session Integration**:
   - Session updates on token refresh
   - Device information tracking
   - Refresh count increments

### iOS Tests

1. **PRF Extension**:
   - PRF output extraction
   - Secure storage and retrieval
   - Expiry handling

2. **Key Derivation**:
   - HKDF key derivation
   - JWK generation
   - Thumbprint calculation
   - Key caching

3. **Proof Generation**:
   - JWT format compliance
   - Signature generation
   - Unique jti per proof
   - DER to raw signature conversion

4. **OAuth Integration**:
   - DPoP header addition
   - Token exchange with DPoP
   - Token refresh with cached key

## Advantages

1. **DPoP Compliant**: Follows RFC 9449 exactly (stable key, unique proofs)
2. **No Server-Side Key Storage**: PRF output derives keys deterministically
3. **Excellent UX**: No passkey prompt on every request (only when PRF cache expires after 15 days)
4. **Strong Device Binding**: Refresh tokens cryptographically bound to device
5. **Session Tracking**: Server-side sessions updated on each refresh
6. **WebView SSO**: Seamless authentication sharing between native and web

## Limitations

1. **iOS 17+ Required**: PRF extension only available on iOS 17 and later
2. **PRF Cache Expiry**: Requires re-authentication every 15 days when cache expires
3. **Platform Authenticator Only**: Requires device with biometric authentication
4. **Single Credential**: Implementation assumes one credential per user (can be extended)

## Migration Path

### Phase 1 (MVP) - ✅ Complete
- PRF integration
- DPoP key derivation
- Basic proof generation/validation
- Device binding on token exchange

### Phase 2 (Enhanced Security) - ✅ Complete
- Replay protection
- Session integration
- Device revocation capability
- Comprehensive validation

### Phase 3 (Advanced Features) - 🚧 In Progress
- WebView SSO
- Multiple device support
- Session management UI enhancements
- Audit logging and monitoring

## References

- [RFC 9449: OAuth 2.0 Demonstrating Proof of Possession (DPoP)](https://datatracker.ietf.org/doc/html/rfc9449)
- [RFC 7638: JSON Web Key (JWK) Thumbprint](https://datatracker.ietf.org/doc/html/rfc7638)
- [WebAuthn PRF Extension Spec](https://w3c.github.io/webauthn/#prf-extension)
- [IdentityServer Documentation](https://docs.duendesoftware.com/identityserver)
- [iOS AuthenticationServices Framework](https://developer.apple.com/documentation/authenticationservices)

## Troubleshooting

### PRF Output Not Available
- Ensure iOS 17+ device
- Verify PRF extension added to request
- Check authenticator supports PRF

### DPoP Validation Failures
- Verify clock synchronization (60s tolerance)
- Check HTTP method/URI match exactly
- Ensure unique jti per proof
- Verify signature algorithm is ES256

### Token Refresh Failures
- Check device binding exists
- Verify thumbprint matches
- Ensure cached key available
- Check refresh token not expired

### Session Not Updating
- Verify DPoPSessionCoordinator registered
- Check dpop_jkt claim in token
- Ensure device binding has refresh token handle

## Next Steps

1. ✅ Apply database migration: `dotnet ef database update`
2. ✅ Build and test server components
3. 🚧 Implement WebView SSO server-side components
4. 🚧 Add session management UI enhancements
5. 🚧 Create comprehensive test suite
6. 🚧 Add audit logging for DPoP events
7. 🚧 Implement device revocation UI
8. 🚧 Add monitoring and alerting

## Support

For issues or questions:
1. Check this documentation
2. Review RFC 9449 specification
3. Examine server logs for DPoP validation details
4. Check iOS console for PRF/DPoP debug output

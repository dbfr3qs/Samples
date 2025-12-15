# DPoP-Based Mobile Sessions with PRF Extension - Implementation Plan

## Executive Summary

This document outlines the implementation plan for extending the native passkey authentication system to support DPoP (Demonstrating Proof-of-Possession) bound refresh tokens using the WebAuthn PRF (Pseudo-Random Function) extension. The implementation will enable device-bound mobile sessions tracked server-side without relying on browser cookies, and includes WebView SSO capabilities for seamless authentication between native and web contexts.

**Key Insight**: In DPoP (RFC 9449), the key material is stable but each proof must be unique (via `jti` and `iat`). This makes PRF-derived keys ideal since we can cache the key and generate fresh proofs without requiring passkey interaction on every request.

## Goals

1. **Device-Bound Tokens**: Cryptographically bind refresh tokens to mobile devices using DPoP
2. **PRF-Based Key Derivation**: Leverage passkey PRF extension to derive stable DPoP key material
3. **Server-Side Session Tracking**: Update IdentityServer sessions on each token refresh
4. **RFC 9449 Compliance**: Strictly adhere to DPoP specification
5. **WebView SSO**: Enable seamless authentication for web content loaded in mobile app using `id_token_hint`
6. **No Passkey Prompts on Refresh**: Cache PRF output to avoid user interaction on every request

## Architecture Overview

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

## Implementation Phases

### Phase 1: PRF Integration in Mobile Flow

**Objective**: Enable PRF extension in passkey registration and authentication, capture PRF output on iOS.

**Server Changes**:
- Modify `MobilePasskeyEndpoints.cs` to include PRF extension in creation/assertion options
- Generate user-specific PRF salt: `SHA256(userId)`
- Store PRF salt with credential
- Create `PrfOutputStore` to temporarily store PRF output during token exchange
- Return PRF extension in endpoint responses

**iOS Changes**:
- Add PRF extension support using `ASAuthorizationPublicKeyCredentialPRFRegistrationInput` and `ASAuthorizationPublicKeyCredentialPRFAssertionInput`
- Extract PRF output from credential responses: `credential.prf?.first`
- Create `SecureStorage` class to cache PRF output in Keychain (15-day expiry)
- Update models to include `extensions` field
- Pass PRF output to authentication completion endpoint

**Key Files**:
- `IdentityServerAspNetIdentityPasskeys/Passkeys/MobilePasskeyEndpoints.cs`
- `IdentityServerAspNetIdentityPasskeys/Services/PrfOutputStore.cs`
- `IdpMobileClient/Sources/IdpMobileClient/PasskeyAuthService.swift`
- `IdpMobileClient/Sources/IdpMobileClient/SecureStorage.swift`

### Phase 2: DPoP Key Derivation

**Objective**: Derive stable P-256 key pairs from PRF output using HKDF, generate JWK representations.

**Database Schema**:
- Create `DeviceBinding` entity with fields: `Id`, `UserId`, `CredentialId`, `DPoPPublicKeyJwk`, `PublicKeyThumbprint`, `RefreshTokenHandle`, `CreatedAt`, `LastUsedAt`, `RefreshCount`
- Create `DeviceBindingStore` for CRUD operations

**iOS Key Derivation**:
- Use HKDF to derive 32-byte key material from PRF output
- Create P-256 private key from derived material
- Generate JWK representation (public key only)
- Calculate JWK thumbprint per RFC 7638 (canonical JSON + SHA-256)
- Store derived key in Keychain for reuse

**Key Files**:
- `IdentityServerAspNetIdentityPasskeys/Models/DeviceBinding.cs`
- `IdentityServerAspNetIdentityPasskeys/Services/DeviceBindingStore.cs`
- `IdpMobileClient/Sources/IdpMobileClient/DPoPKeyManager.swift`
- `IdpMobileClient/Sources/IdpMobileClient/Extensions/Data+Base64URL.swift`

### Phase 3: DPoP Proof Generation (RFC 9449 Compliant)

**Objective**: Generate spec-compliant DPoP proofs with unique jti/iat per request using stable key.

**iOS Implementation**:
- Create `DPoPProofGenerator` class
- Generate JWT with header: `{"typ":"dpop+jwt","alg":"ES256","jwk":{...}}`
- Generate payload with: `jti` (UUID), `htm` (HTTP method), `htu` (HTTP URI), `iat` (timestamp)
- Optionally include `ath` (access token hash) for API requests
- Sign with P-256 key using ES256 algorithm
- Convert DER signature to raw format (r || s)
- Integrate with `OAuthClient` for token exchange and refresh

**Key Features**:
- Each proof is unique (new `jti` and `iat`)
- Same key is reused (no passkey prompt)
- Proofs expire after 60 seconds (timestamp validation)

**Key Files**:
- `IdpMobileClient/Sources/IdpMobileClient/DPoPProofGenerator.swift`
- `IdpMobileClient/Sources/IdpMobileClient/OAuthClient.swift` (modified)

### Phase 4: Server-Side DPoP Validation

**Objective**: Validate DPoP proofs per RFC 9449 with replay protection.

**Validation Steps**:
1. Parse JWT and validate format
2. Check `typ` header is `dpop+jwt`
3. Check `alg` header is `ES256`
4. Extract JWK from header and validate (EC P-256)
5. Verify signature using JWK public key
6. Validate required claims: `jti`, `htm`, `htu`, `iat`
7. Check `htm` matches HTTP method
8. Check `htu` matches request URI (scheme, host, port, path)
9. Validate timestamp (allow 60 second skew)
10. Check `jti` not already used (replay protection)
11. Validate `ath` if access token present
12. Calculate JWK thumbprint

**Token Endpoint Integration**:
- Create `DPoPTokenRequestValidator` implementing `ICustomTokenRequestValidator`
- For authorization code grant: Create device binding, store thumbprint
- For refresh token grant: Verify device binding, check thumbprint match, update usage stats
- Add `dpop_jkt` claim to token

**Key Files**:
- `IdentityServerAspNetIdentityPasskeys/Services/DPoPProofValidator.cs`
- `IdentityServerAspNetIdentityPasskeys/Services/ReplayCache.cs`
- `IdentityServerAspNetIdentityPasskeys/Services/DPoPTokenRequestValidator.cs`

### Phase 5: Server-Side Session Integration

**Objective**: Update server-side sessions with device activity on each token refresh.

**Implementation**:
- Create `DPoPSessionCoordinator` implementing `ISessionCoordinationService`
- On token refresh, extract `dpop_jkt` from claims
- Update session with custom properties: `dpop_jkt`, `device_id`, `last_refresh`, `refresh_count`
- Enhance session management UI to display device information
- Add device revocation capability

**Key Files**:
- `IdentityServerAspNetIdentityPasskeys/Services/DPoPSessionCoordinator.cs`
- `IdentityServerAspNetIdentityPasskeys/Pages/ServerSideSessions/Index.cshtml` (modified)

### Phase 6: WebView SSO Flow

**Objective**: Enable seamless authentication for web content loaded in mobile app.

**Architecture**:
1. iOS app loads web page in WKWebView
2. Web page initiates OAuth code flow
3. iOS app intercepts redirect to IdentityServer
4. iOS app adds `id_token_hint` parameter (from native authentication)
5. iOS app adds `prompt=none` for silent authentication
6. IdentityServer validates `id_token_hint`, checks for active session
7. If valid session exists, IdentityServer issues authorization code without user interaction
8. Web page exchanges code for tokens
9. Web page sets its own session cookie

**iOS Implementation**:
- Create `AuthenticatedWebView` class extending `WKWebView`
- Implement `WKNavigationDelegate` to intercept navigation
- Detect navigation to IdentityServer authorize endpoint
- Inject `id_token_hint` and `prompt=none` parameters
- Handle `login_required` error gracefully

**Server Implementation**:
- Extend `AuthorizeInteractionResponseGenerator`
- Validate `id_token_hint` JWT
- Check for active server-side session matching subject
- Allow silent authentication if session exists
- Return `login_required` error if no session

**Client Configuration**:
- Add new client `web-in-app` with `RequireClientSecret = false`
- Enable `id_token_hint` support
- Configure session management for front/back channel logout

**Key Files**:
- `IdpMobileClient/Sources/IdpMobileClient/AuthenticatedWebView.swift`
- `IdentityServerAspNetIdentityPasskeys/Services/AuthorizeInteractionResponseGenerator.cs` (custom)
- `IdentityServerAspNetIdentityPasskeys/Config.cs` (add web-in-app client)

### Phase 7: Testing & Validation

**DPoP Flow Tests**:
1. Initial authentication with PRF
2. Token refresh with DPoP proof (verify unique jti)
3. Replay attack prevention (reuse proof)
4. Key mismatch (different DPoP key)
5. Proof validation (invalid htm, htu, iat, missing jti)
6. Session updates on refresh

**WebView SSO Tests**:
1. Silent authentication with active session
2. Login required without session
3. Session sharing between native and web
4. Logout coordination (back-channel)

**Test Files**:
- `IdpMobileClient/Tests/DPoPTests.swift`
- `IdentityServerAspNetIdentityPasskeys.Tests/DPoPValidationTests.cs`

## Technical Considerations

### Advantages

1. **DPoP Compliant**: Follows RFC 9449 exactly (stable key, unique proofs)
2. **No Server-Side Key Storage**: PRF output derives keys deterministically
3. **Excellent UX**: No passkey prompt on every request (only when PRF cache expires after 15 days)
4. **Strong Device Binding**: Refresh tokens cryptographically bound to device
5. **Session Tracking**: Server-side sessions updated on each refresh
6. **WebView SSO**: Seamless authentication sharing between native and web

### Challenges & Mitigations

1. **PRF Output Caching**
   - Challenge: PRF only available during passkey ceremony
   - Mitigation: Cache PRF output in Keychain with 15-day expiry
   - Trade-off: Require re-authentication when cache expires

2. **Key Derivation Consistency**
   - Challenge: Must derive same key from same PRF output
   - Solution: Use HKDF with fixed salt/info parameters
   - Implementation: CryptoKit's HKDF + P256.Signing.PrivateKey

3. **iOS Crypto Limitations**
   - Challenge: No built-in JWT library
   - Solution: Manual JWT construction with CryptoKit
   - Implementation: Base64URL encoding, JSON serialization, ECDSA signing

4. **Replay Protection**
   - Challenge: Prevent reuse of DPoP proofs
   - Solution: Track used `jti` values in distributed cache (5-minute window)
   - Implementation: Redis or in-memory cache

5. **Multiple Devices**
   - Challenge: User may have multiple devices
   - Solution: Support multiple device bindings per user
   - Implementation: Store credential ID with device binding

## Configuration Requirements

### IdentityServer

```csharp
// In HostingExtensions.cs
builder.Services.AddScoped<IPrfOutputStore, PrfOutputStore>();
builder.Services.AddScoped<DPoPProofValidator>();
builder.Services.AddScoped<IReplayCache, ReplayCache>();
builder.Services.AddScoped<IDeviceBindingStore, DeviceBindingStore>();
builder.Services.AddTransient<ICustomTokenRequestValidator, DPoPTokenRequestValidator>();
builder.Services.AddTransient<ISessionCoordinationService, DPoPSessionCoordinator>();
builder.Services.AddDistributedMemoryCache(); // Or Redis for production
```

### Mobile Client Configuration

```swift
// In Config.swift or similar
let idpBaseURL = "https://idp.dev.internal"
let clientId = "mobile-client"
let redirectUri = "com.idp.mobile://callback"
let scopes = ["openid", "profile", "api", "offline_access"]
```

### Database Migration

```bash
# Add migration for DeviceBinding entity
dotnet ef migrations add AddDeviceBinding
dotnet ef database update
```

## Security Best Practices

1. **PRF Salt Management**: Use user-specific salts derived from user ID
2. **Key Storage**: Store derived keys in Keychain with appropriate access control
3. **Proof Expiration**: Enforce 60-second timestamp tolerance
4. **Replay Protection**: Track used JTIs for at least 5 minutes
5. **Session Binding**: Verify DPoP key matches device binding on every refresh
6. **Revocation**: Implement device revocation capability in session management UI
7. **Audit Logging**: Log all DPoP validation failures and device binding changes

## Migration Path

### Phase 1 (MVP)
- PRF integration
- DPoP key derivation
- Basic proof generation/validation
- Device binding on token exchange

### Phase 2 (Enhanced Security)
- Replay protection
- Session integration
- Device revocation
- Comprehensive validation

### Phase 3 (Advanced Features)
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

## Next Steps

1. Review and approve this plan
2. Set up development environment
3. Create feature branch
4. Implement Phase 1 (PRF Integration)
5. Test PRF functionality on iOS 17+
6. Proceed with subsequent phases
7. Comprehensive testing before production deployment

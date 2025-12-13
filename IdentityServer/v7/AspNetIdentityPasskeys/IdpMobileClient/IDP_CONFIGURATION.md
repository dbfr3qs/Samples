# IdentityServer Configuration for Mobile Client

This guide explains how to configure your IdentityServer instance to work with the iOS mobile client.

## OAuth Client Configuration

Add the following client configuration to your IdentityServer:

```csharp
new Client
{
    ClientId = "mobile-client",
    ClientName = "iOS Mobile Client",
    
    AllowedGrantTypes = GrantTypes.Code,
    RequirePkce = true,
    RequireClientSecret = false, // Public client
    
    RedirectUris = { "com.idp.mobile://callback" },
    PostLogoutRedirectUris = { "com.idp.mobile://callback" },
    
    AllowedScopes = {
        IdentityServerConstants.StandardScopes.OpenId,
        IdentityServerConstants.StandardScopes.Profile,
        IdentityServerConstants.StandardScopes.Email,
        "api",
        IdentityServerConstants.StandardScopes.OfflineAccess
    },
    
    AllowOfflineAccess = true, // Enable refresh tokens
    RefreshTokenUsage = TokenUsage.ReUse,
    RefreshTokenExpiration = TokenExpiration.Sliding,
    SlidingRefreshTokenLifetime = 1296000, // 15 days
    AbsoluteRefreshTokenLifetime = 2592000, // 30 days
    
    AccessTokenLifetime = 3600, // 1 hour
    
    // Allow passkey authentication
    AllowedCorsOrigins = { "https://idp.dev.internal:5000" }
}
```

## WebAuthn/Passkey Endpoints

Your IdentityServer must expose WebAuthn endpoints for passkey authentication. The mobile client expects:

### Registration Endpoints

**POST** `/api/passkey/register/begin`
- Request body: `{ "username": "user@example.com" }`
- Response: WebAuthn registration options

```json
{
  "challenge": "base64url-encoded-challenge",
  "rp": {
    "name": "Identity Provider",
    "id": "idp.dev.internal"
  },
  "user": {
    "id": "user-id",
    "name": "user@example.com",
    "displayName": "User Name"
  },
  "timeout": 60000
}
```

**POST** `/api/passkey/register/complete`
- Request body: WebAuthn registration credential
- Response: Success/failure

### Authentication Endpoints

**POST** `/api/passkey/authenticate/begin`
- Request body: Empty or `{}`
- Response: WebAuthn authentication options

```json
{
  "challenge": "base64url-encoded-challenge",
  "timeout": 60000,
  "rpId": "idp.dev.internal"
}
```

**POST** `/api/passkey/authenticate/complete`
- Request body: WebAuthn assertion credential
- Response: Authorization code

```json
{
  "code": "authorization-code",
  "state": "optional-state"
}
```

## CORS Configuration

Ensure CORS is configured to allow requests from the mobile client:

```csharp
services.AddCors(options =>
{
    options.AddPolicy("MobileClient", policy =>
    {
        policy.WithOrigins("capacitor://localhost", "ionic://localhost")
              .AllowAnyHeader()
              .AllowAnyMethod()
              .AllowCredentials();
    });
});
```

## Associated Domains (iOS)

For passkey authentication to work seamlessly, configure associated domains:

1. Create `.well-known/apple-app-site-association` file on your IdP domain:

```json
{
  "webcredentials": {
    "apps": [
      "TEAMID.com.idp.mobile.demo"
    ]
  }
}
```

2. Serve this file at `https://idp.dev.internal:5000/.well-known/apple-app-site-association`
3. Ensure it's served with `Content-Type: application/json`
4. No file extension required

## SSL/TLS Configuration

For development with `.dev.internal` domains:

1. Generate a self-signed certificate or use mkcert:
   ```bash
   mkcert idp.dev.internal api.dev.internal
   ```

2. Install the root CA on your iOS device:
   - Email the root CA certificate to yourself
   - Open on iOS device and install profile
   - Settings → General → About → Certificate Trust Settings
   - Enable full trust for the root certificate

3. Configure your IdP to use the certificate

## Testing the Configuration

### Test OAuth Flow

```bash
# Test authorization endpoint
curl -X GET "https://idp.dev.internal:5000/connect/authorize?client_id=mobile-client&redirect_uri=com.idp.mobile://callback&response_type=code&scope=openid%20profile%20email%20api%20offline_access&code_challenge=CHALLENGE&code_challenge_method=S256&state=STATE"

# Test token endpoint
curl -X POST "https://idp.dev.internal:5000/connect/token" \
  -H "Content-Type: application/x-www-form-urlencoded" \
  -d "grant_type=authorization_code&code=CODE&redirect_uri=com.idp.mobile://callback&client_id=mobile-client&code_verifier=VERIFIER"
```

### Test Passkey Endpoints

```bash
# Test registration begin
curl -X POST "https://idp.dev.internal:5000/api/passkey/register/begin" \
  -H "Content-Type: application/json" \
  -d '{"username":"test@example.com"}'

# Test authentication begin
curl -X POST "https://idp.dev.internal:5000/api/passkey/authenticate/begin" \
  -H "Content-Type: application/json" \
  -d '{}'
```

## Troubleshooting

### "Invalid redirect_uri"
- Verify `com.idp.mobile://callback` is in the client's `RedirectUris`
- Check for typos or extra whitespace

### "Invalid client"
- Ensure `ClientId = "mobile-client"` matches exactly
- Verify client is enabled

### "Unauthorized client"
- Check `AllowedGrantTypes` includes `Code`
- Verify `RequirePkce = true`

### Passkey authentication fails
- Verify WebAuthn endpoints are accessible
- Check that `rpId` matches your domain
- Ensure associated domains are configured correctly

### Refresh token not returned
- Verify `AllowOfflineAccess = true`
- Check that `offline_access` scope is requested
- Ensure `AllowedScopes` includes `IdentityServerConstants.StandardScopes.OfflineAccess`

## Security Considerations

1. **PKCE**: Always required for mobile clients (public clients)
2. **Client Secret**: Not used for mobile apps (can't be kept secret)
3. **Redirect URI**: Use custom URL scheme, validate strictly
4. **Refresh Tokens**: Use sliding expiration, rotate on use
5. **Token Storage**: iOS Keychain provides secure storage
6. **Certificate Pinning**: Consider implementing for production
7. **Jailbreak Detection**: Consider implementing for high-security apps

## Production Checklist

- [ ] Replace `dev.internal` domains with production domains
- [ ] Use valid SSL certificates (not self-signed)
- [ ] Configure associated domains with production Team ID
- [ ] Set appropriate token lifetimes
- [ ] Enable certificate pinning
- [ ] Implement proper error handling and logging
- [ ] Test on physical iOS devices
- [ ] Verify passkey sync across devices
- [ ] Test token refresh flow
- [ ] Test logout and token revocation

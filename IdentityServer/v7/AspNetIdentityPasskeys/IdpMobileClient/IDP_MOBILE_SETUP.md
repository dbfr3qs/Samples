# IdP Setup for Mobile Passkey Authentication

## Problem

Your IdentityServer has passkey support, but the endpoints are designed for **web-based authentication** where users are already logged in. The mobile app needs **API endpoints** that work with the OAuth/OIDC flow.

## Solution

I've created new mobile-friendly passkey endpoints at:
- `POST /api/passkey/authenticate/begin` - Start passkey authentication
- `POST /api/passkey/authenticate/complete` - Complete passkey authentication and get authorization code

## Files Added/Modified

### 1. New File: `MobilePasskeyEndpoints.cs`

Location: `/Users/chris.keogh/dev/Samples/IdentityServer/v7/AspNetIdentityPasskeys/IdentityServerAspNetIdentityPasskeys/Passkeys/MobilePasskeyEndpoints.cs`

This file contains the mobile passkey API endpoints.

### 2. Modified: `HostingExtensions.cs`

Changes:
- Added session support (line 60-66)
- Added `app.UseSession()` (line 137)
- Added `app.MapMobilePasskeyEndpoints()` (line 143)

## Steps to Enable

### 1. Rebuild the IdP

```bash
cd /Users/chris.keogh/dev/Samples/IdentityServer/v7/AspNetIdentityPasskeys/IdentityServerAspNetIdentityPasskeys
dotnet build
```

### 2. Restart the IdP

Stop the current IdP process and restart it:

```bash
dotnet run
```

### 3. Test the Endpoint

```bash
curl -X POST https://idp.dev.internal:5001/api/passkey/authenticate/begin \
  -H "Content-Type: application/json" \
  -d '{}' \
  --insecure

# Should return WebAuthn options with challenge
```

### 4. Test from Mobile App

Once the IdP is restarted:
1. Rebuild the mobile app in Xcode
2. Run on simulator
3. Tap "Sign in with Passkey"
4. Should now get past the 404 error

## Current Limitations

The current implementation is a **proof of concept**. It has these limitations:

### ⚠️ Authorization Code Generation

The `/authenticate/complete` endpoint currently returns a temporary code (`temp_auth_code`). For production, you need to:

1. **Generate a real authorization code** using IdentityServer's authorization code service
2. **Store it** with the user's session and PKCE parameters
3. **Return it** to the mobile app
4. Mobile app **exchanges it** for tokens via `/connect/token`

### ⚠️ Session Management

- Uses in-memory sessions (not suitable for load-balanced scenarios)
- Challenge stored in session (works for single server)
- Consider using distributed cache for production

### ⚠️ CORS

You may need to add CORS support for mobile apps:

```csharp
// In ConfigureServices
builder.Services.AddCors(options =>
{
    options.AddPolicy("MobileApp", policy =>
    {
        policy.WithOrigins("capacitor://localhost", "ionic://localhost")
              .AllowAnyHeader()
              .AllowAnyMethod()
              .AllowCredentials();
    });
});

// In ConfigurePipeline
app.UseCors("MobileApp");
```

## Next Steps for Production

### 1. Proper Authorization Code Flow

Replace the temporary code generation with proper IdentityServer integration:

```csharp
// Use IAuthorizationCodeStore to generate and store authorization codes
var authorizationCode = await authorizationCodeStore.StoreAuthorizationCodeAsync(new AuthorizationCode
{
    ClientId = "mobile-client",
    Subject = new ClaimsPrincipal(new ClaimsIdentity(claims)),
    RequestedScopes = new[] { "openid", "profile", "email", "api", "offline_access" },
    CodeChallenge = codeChallenge,
    CodeChallengeMethod = "S256",
    // ... other properties
});
```

### 2. PKCE Integration

The mobile app sends:
- `code_challenge` in the initial request
- `code_verifier` when exchanging the code

The IdP must:
- Store the `code_challenge` with the authorization code
- Verify the `code_verifier` matches when exchanging for tokens

### 3. Client Configuration

Ensure your IdentityServer has the mobile client configured:

```csharp
new Client
{
    ClientId = "mobile-client",
    ClientName = "iOS Mobile Client",
    AllowedGrantTypes = GrantTypes.Code,
    RequirePkce = true,
    RequireClientSecret = false,
    RedirectUris = { "com.idp.mobile://callback" },
    AllowedScopes = { "openid", "profile", "email", "api", "offline_access" },
    AllowOfflineAccess = true
}
```

### 4. Testing Checklist

- [ ] IdP rebuilt and restarted
- [ ] `/api/passkey/authenticate/begin` returns 200 OK
- [ ] Response includes `challenge`, `rpId`, `timeout`
- [ ] Mobile app can call the endpoint
- [ ] Passkey prompt appears in simulator
- [ ] Authentication completes successfully
- [ ] Authorization code is returned
- [ ] Code can be exchanged for tokens

## Alternative Approach: Use Existing Web Flow

Instead of creating custom API endpoints, you could:

1. **Use the standard OAuth authorize endpoint** with passkey authentication
2. **Embed a web view** in the mobile app
3. **Let the user authenticate** via the web interface
4. **Capture the authorization code** from the redirect

This approach:
- ✅ Uses existing, tested code
- ✅ No custom endpoints needed
- ✅ Proper OAuth flow
- ❌ Requires web view (not native passkey experience)
- ❌ Less seamless UX

## Debugging

### Check if endpoints are registered

```bash
# Should show the new endpoints
curl -v https://idp.dev.internal:5001/api/passkey/authenticate/begin 2>&1 | grep "HTTP/"
```

### View IdP logs

```bash
# Watch for errors
tail -f /path/to/idp/logs/diagnostic.log
```

### Test with verbose curl

```bash
curl -v -X POST https://idp.dev.internal:5001/api/passkey/authenticate/begin \
  -H "Content-Type: application/json" \
  -d '{}' \
  --insecure 2>&1 | tee test-output.log
```

## Summary

The mobile passkey endpoints are now **added to your IdP code** but you need to:

1. **Rebuild** the IdP project
2. **Restart** the IdP server
3. **Test** the new endpoints
4. **Rebuild** the mobile app
5. **Test** the full flow

The current implementation is a **starting point** that needs proper authorization code generation for production use.

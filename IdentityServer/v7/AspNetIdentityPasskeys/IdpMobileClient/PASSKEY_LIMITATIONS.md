# Passkey Limitations in Development

## The Problem

**Passkeys on iOS require valid SSL certificates for domain association.** Self-signed certificates (like ASP.NET Core development certificates) are not sufficient, even if trusted in the simulator's keychain.

### Why This Happens

1. **Domain Association Verification**: iOS validates the AASA file over HTTPS
2. **Certificate Validation**: iOS requires a certificate signed by a trusted CA
3. **Self-Signed Certificates**: Development certificates fail this validation
4. **No Bypass**: There's no development mode to skip this requirement

### Error You'll See

```
Error Domain=com.apple.AuthenticationServices.AuthorizationError Code=1004
"Unable to verify webcredentials association of GSSMQP88ZN.com.idp.mobile.demo with domain idp.dev.internal"
```

## Solutions

### Option 1: Use OAuth PKCE Flow (Recommended for Development)

The mobile app already supports standard OAuth with PKCE. You can test authentication without passkeys:

**Current Implementation:**
- OAuth client ID: `mobile.client` (needs to be configured in `Config.cs`)
- Uses authorization code flow with PKCE
- Works with self-signed certificates

**To Test:**
1. Configure the OAuth client in IdentityServer
2. Use the standard sign-in flow (not passkeys)
3. Test passkeys separately in the web application

### Option 2: Use a Tunneling Service (ngrok, Cloudflare Tunnel)

Expose your local server through a public domain with valid SSL:

**Using ngrok:**
```bash
# Install ngrok
brew install ngrok

# Start tunnel
ngrok http https://localhost:5001

# You'll get a URL like: https://abc123.ngrok.io
```

**Update configuration:**
1. Set `idpBaseURL` to the ngrok URL
2. Set `relyingPartyIdentifier` to the ngrok domain
3. Update AASA file with the ngrok domain
4. Update entitlements with the ngrok domain

**Limitations:**
- Free ngrok URLs change on each restart
- Requires internet connection
- Adds latency

### Option 3: Use a Real Domain

If you have a domain with valid SSL:

1. **Point domain to localhost:**
   ```
   # /etc/hosts
   127.0.0.1 myapp.example.com
   ```

2. **Get a valid certificate:**
   - Use Let's Encrypt with DNS challenge
   - Or use a wildcard cert for `*.example.com`

3. **Configure IdentityServer** to use the domain

4. **Update mobile app** with the real domain

### Option 4: Test on Physical Device with Production Server

Deploy to a staging server with valid SSL and test on a physical device.

## What Works Without Valid Certificates

✅ **OAuth PKCE Flow** - Standard authentication works fine
✅ **API Calls** - With certificate trust exceptions
✅ **Web Passkeys** - Browsers handle cert trust differently
❌ **Mobile Passkeys** - Requires valid certificates

## Recommended Development Workflow

1. **Develop OAuth flow** with self-signed certs (works fine)
2. **Test passkeys in web app** (works with trusted dev cert in browser)
3. **Test mobile passkeys** on staging with valid SSL
4. **Or use ngrok** for occasional passkey testing

## Production Checklist

For production, passkeys will work fine if you have:

- [ ] Valid SSL certificate from trusted CA
- [ ] Public domain name
- [ ] AASA file accessible at `https://yourdomain.com/.well-known/apple-app-site-association`
- [ ] Correct Team ID and Bundle ID in AASA file
- [ ] Associated Domains capability in app
- [ ] Domain listed in entitlements: `webcredentials:yourdomain.com`

## Alternative: Configure OAuth Client for Mobile

Since passkeys won't work in development, let's ensure the OAuth flow works:

### Add Mobile Client to Config.cs

```csharp
new Client
{
    ClientId = "mobile.client",
    ClientName = "Mobile Client",
    
    AllowedGrantTypes = GrantTypes.Code,
    RequireClientSecret = false, // Public client (mobile app)
    RequirePkce = true,
    
    RedirectUris = { "com.idp.mobile://callback" },
    PostLogoutRedirectUris = { "com.idp.mobile://callback" },
    
    AllowOfflineAccess = true,
    AllowedScopes = { "openid", "profile", "api", "offline_access" }
}
```

### Test OAuth Flow

The mobile app's `OAuthClient` already implements this:
1. Generates PKCE challenge
2. Opens authorization URL
3. Exchanges code for tokens
4. Stores tokens securely

This works perfectly with self-signed certificates (with ATS exceptions).

## Summary

**For Development:**
- Use OAuth PKCE flow for mobile authentication
- Test passkeys in the web application
- Passkeys on mobile require production-like setup

**For Production:**
- Passkeys will work fine with valid SSL
- No code changes needed
- Just proper SSL and domain configuration

The mobile app code is ready for passkeys - it just needs a proper SSL environment to work.

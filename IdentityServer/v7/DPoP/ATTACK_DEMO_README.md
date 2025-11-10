# OAuth DPoP Browser Swapping Attack Demo

## Overview

This demo demonstrates a browser swapping attack that bypasses DPoP (Demonstrating Proof of Possession) protection in OAuth flows. The attack exploits a scenario where malicious JavaScript can intercept and manipulate the OAuth authorization flow.

## Attack Flow

### High-Level Overview

This attack demonstrates how an attacker can bypass DPoP protection by coordinating between their browser and a victim's compromised browser. The key insight is that while DPoP binds tokens to a specific key, if the attacker can control the authorization request parameters AND obtain the resulting authorization code, they can complete the flow.

### Detailed Attack Steps

1. **Attacker Phase 1 - Capture OAuth Parameters**: 
   - The attacker initiates a legitimate OAuth flow in their browser
   - When redirected to the authorization endpoint, the URL contains:
     - `dpop_jkt`: The DPoP key thumbprint (binds the flow to attacker's key)
     - `state`: OAuth state parameter
     - `code_challenge`: PKCE challenge (attacker has the matching verifier)
     - `nonce`: OpenID Connect nonce for replay protection
   - The attacker manually copies these parameters from the URL

2. **Attacker Phase 2 - Send Parameters to Victim**:
   - The attacker submits all captured parameters to the AttackerApi
   - These parameters are stored and made available to the victim's browser

3. **Victim Phase 1 - Receive Stolen Parameters**:
   - The victim's browser (compromised with malicious JavaScript) polls the AttackerApi
   - Once available, it receives all the attacker's OAuth parameters

4. **Victim Phase 2 - Silent Authorization Request**:
   - The victim's browser constructs a new authorization request using:
     - The attacker's `dpop_jkt` (binds to attacker's DPoP key)
     - The attacker's `code_challenge` (attacker has the verifier)
     - The attacker's `nonce` (for proper ID token validation)
   - Since the victim is already authenticated with the Authorization Server, the request succeeds silently
   - The authorization code is issued bound to the attacker's DPoP key

5. **Victim Phase 3 - Intercept Authorization Code**:
   - When the authorization code is returned, malicious JavaScript intercepts it
   - The code is sent to the AttackerApi before the victim's browser can use it

6. **Attacker Phase 3 - Complete Token Exchange**:
   - The attacker retrieves the stolen authorization code from AttackerApi
   - The attacker exchanges the code for tokens using:
     - Their DPoP key (matches the `dpop_jkt` from step 1)
     - Their `code_verifier` (matches the `code_challenge` from step 1)
   - The token exchange succeeds because all cryptographic bindings match
   - The attacker is now authenticated as the victim!

## Architecture

### Services

- **IdentityServerHost** (port 5001): The Authorization Server
- **WebClient** (port 5010): The target application (OAuth client)
- **Api** (port 5005): Protected API resource
- **AttackerApi** (port 7666): Attacker's server for coordinating the attack

### Key Files

- `AttackerApi/wwwroot/attack.js`: Malicious JavaScript that intercepts OAuth flows
- `AttackerApi/Program.cs`: API endpoints for storing/retrieving stolen credentials
- `WebClient/Views/Home/AttackDemo.cshtml`: Attacker's UI
- `WebClient/Views/Home/AttackVictim.cshtml`: Victim's UI (simulates compromised browser)

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
7. Copy the following parameters from the URL:
   - `dpop_jkt=...` (e.g., `NzbLsXh8uDCcd-6MNwXF4W_7noWXFZAfHkxZsRGC9Xs`)
   - `state=...` (long base64 string)
   - `code_challenge=...` (base64url string)
   - `nonce=...` (numeric timestamp)
8. Return to the AttackDemo page (use browser back button)
9. Paste each parameter into the corresponding input field
10. Click **"Submit Parameters"** - all fields should turn green
11. The parameters are now available to the victim's browser via the AttackerApi

### Step 3: Set Up Victim Browser

1. Open a **different browser window or profile** (e.g., Firefox or Chrome Incognito)
   - This simulates a different user's browser that has been compromised

2. **First, establish a victim session with IdentityServer:**
   - Navigate to: `https://localhost:5010/Home/Secure`
   - Log in with the victim's credentials (e.g., **alice/alice**)
   - You should see the "Secure" page confirming you're logged in
   - This creates an active session with the Authorization Server
   - **Important**: Keep this browser window open and logged in

3. **Now simulate the compromised browser:**
   - In the same browser, navigate to: `https://localhost:5010/Home/AttackVictim`
   - Click **"Start Victim Simulation"**
   - The status should show "Polling for DPoP proof..."

4. **Wait for stolen parameters:**
   - The victim's browser automatically polls the AttackerApi
   - Once the attacker's parameters are detected (from Step 2), you'll see:
     - "✅ Stolen DPoP JKT received!"
     - The attacker's session ID
     - A truncated view of the stolen parameters

5. **Trigger the silent OAuth flow:**
   - Click **"Manually Authenticate"**
   - You'll be redirected to `/Home/Secure` (or may see a brief authorization screen)
   - Since you're already logged in, the authorization happens automatically
   - The malicious JavaScript intercepts the authorization code
   - You'll see: "🚨 INTERCEPTED Authorization Code!"
   - The code is automatically sent to the AttackerApi
   - Status changes to "Code Exfiltrated!"

### Step 4: Complete the Attack

1. **Return to the attacker browser** (from Step 2)

2. **Verify the stolen code is available:**
   - Click **"Check Status"**
   - You should see JSON showing the stolen authorization code
   - Example: `{"sessionId": "...", "status": "code_stored", ...}`

3. **Complete the attack:**
   - Click **"Complete Login with Stolen Code"**
   - The attacker's browser will:
     - Retrieve the stolen authorization code from AttackerApi
     - Inject it into the OAuth callback URL with the attacker's state
     - The OIDC middleware will:
       - Validate the state (matches attacker's correlation cookie)
       - Exchange the code for tokens using:
         - The stolen authorization code
         - The attacker's `code_verifier` (from correlation cookie)
         - The attacker's DPoP key (matches the `dpop_jkt`)
       - Validate the ID token nonce (matches attacker's nonce)

4. **Attack successful!**
   - You'll be redirected to `/Home/Secure`
   - The attacker is now authenticated as the victim (alice)
   - The attacker has valid access tokens bound to their DPoP key
   - The attacker can now call APIs on behalf of the victim

## Important Notes

### Session ID Coordination

The attack requires both the attacker and victim to use the **same Session ID**. In the current implementation:

- Each browser generates its own random Session ID
- You need to manually copy the Session ID from the attacker browser to the victim browser
- In a real attack, this would be coordinated through the malicious JavaScript

### Browser Sessions

The victim must have an active session with the Authorization Server (IdentityServerHost) for the attack to work. This is because:

- The victim's browser needs to be already authenticated
- When the victim initiates the OAuth flow, the AS recognizes the existing session
- The authorization code is issued without requiring re-authentication

### CORS Configuration

The AttackerApi has CORS enabled to allow cross-origin requests from the WebClient. This simulates a scenario where the attacker's server can receive data from the compromised client.

## Security Implications

### Why DPoP Doesn't Prevent This Attack

This attack demonstrates that **DPoP alone is not sufficient** to prevent all OAuth attacks. The attack succeeds because:

1. **DPoP Binds to the Wrong Browser**: 
   - DPoP successfully binds the tokens to a specific key
   - However, the attacker controls which key is used from the start
   - The authorization code is issued bound to the attacker's DPoP key, not the victim's

2. **JavaScript Access**: 
   - Malicious JavaScript can read and manipulate authorization requests
   - It can intercept authorization codes before the legitimate flow completes
   - It can exfiltrate data to external servers

3. **Session Reuse**: 
   - The victim's existing Authorization Server session is leveraged
   - No re-authentication is required if the victim is already logged in
   - The AS doesn't know the request came from malicious JavaScript

4. **PKCE Doesn't Help**:
   - The attacker generates their own `code_verifier` and `code_challenge`
   - The victim's browser uses the attacker's `code_challenge` in the authorization request
   - The attacker has the matching `code_verifier` for token exchange
   - PKCE validation passes because the attacker controls both sides

5. **All Cryptographic Bindings Match**:
   - `dpop_jkt` → Attacker's DPoP key
   - `code_challenge` → Attacker's PKCE verifier
   - `nonce` → Attacker's nonce (for ID token validation)
   - `state` → Attacker's state (for CSRF protection)
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

### 2. **Pushed Authorization Requests (PAR)**
- Require clients to push authorization parameters to the AS via a back-channel
- The AS returns a request URI that's used in the front-channel
- Malicious JavaScript cannot modify the authorization parameters
- **This is the most effective mitigation for this specific attack**

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

### Parameters Not Showing in URL

If you don't see the OAuth parameters in the authorize URL:
- Make sure you clicked "Start OAuth Flow" in the AttackDemo page
- Check that you're looking at the IdentityServer URL (port 5001)
- The URL should contain: `dpop_jkt`, `state`, `code_challenge`, `nonce`

### Victim Browser Not Receiving Parameters

If the victim's browser shows "ERROR: dpop_jkt not found":
- Verify you submitted the parameters in the attacker browser (Step 2.10)
- Check the AttackerApi logs for successful storage
- Try clicking "Check Status" in the attacker browser to verify data is stored

### Authorization Code Not Intercepted

If the victim's browser doesn't intercept the code:
- Ensure the victim is logged in to IdentityServer first (Step 3.2)
- Check browser console for JavaScript errors
- Verify the malicious script is loaded from `https://localhost:7666/attack.js`

### Nonce Validation Error

If you see "OpenIdConnectProtocolInvalidNonceException":
- Ensure you captured and submitted the `nonce` parameter from the authorize URL
- Verify the nonce is being sent to the AttackerApi (check the JSON payload)
- Confirm the victim's browser is using the stolen nonce in the silent auth request

### Certificate Errors

You may need to accept self-signed certificates for all services:
- IdentityServerHost: `https://localhost:5001`
- Api: `https://localhost:5005`
- WebClient: `https://localhost:5010`
- AttackerApi: `https://localhost:7666`

Visit each URL directly and accept the certificate warning.

## Educational Purpose Only

This demonstration is for educational and security research purposes only. Do not use these techniques against systems you don't own or have explicit permission to test.

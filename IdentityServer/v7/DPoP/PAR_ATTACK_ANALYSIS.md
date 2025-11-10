# Why PAR (Pushed Authorization Requests) Doesn't Prevent This Attack

## Executive Summary

This demonstration proves that **Pushed Authorization Requests (PAR) alone does NOT prevent the browser swapping attack** when:
1. The `request_uri` is exposed in the front-channel (browser URL)
2. The `request_uri` can be reused by a different browser
3. Malicious JavaScript can intercept the authorization code using `response_mode=fragment`

## The Attack Flow with PAR

### Step 1: Attacker Initiates Legitimate OAuth Flow

```
Attacker's Browser → WebClient Backend → Authorization Server
                                         (PAR Request)
```

1. Attacker navigates to `/Home/Secure` in their browser
2. WebClient backend makes PAR request with attacker's parameters:
   ```
   POST /connect/par
   Authorization: Basic <client_credentials>
   
   client_id=dpop
   &redirect_uri=https://localhost:5010/signin-oidc
   &response_type=code
   &scope=openid profile
   &dpop_jkt=<ATTACKER_KEY_THUMBPRINT>      ← Attacker's DPoP key!
   &code_challenge=<ATTACKER_CHALLENGE>      ← Attacker has verifier!
   &nonce=<ATTACKER_NONCE>                   ← Attacker's nonce!
   &state=<ATTACKER_STATE>                   ← Attacker's state!
   ```

3. Authorization Server returns:
   ```json
   {
     "request_uri": "urn:ietf:params:oauth:request_uri:6esc_11ACC5bwc014ltc14eY22c",
     "expires_in": 60
   }
   ```

4. Attacker's browser is redirected to:
   ```
   https://localhost:5001/connect/authorize
     ?client_id=dpop
     &request_uri=urn:ietf:params:oauth:request_uri:6esc_11ACC5bwc014ltc14eY22c
     &state=<ATTACKER_STATE>
   ```

### Step 2: Attacker Captures request_uri

⚠️ **Critical vulnerability**: The `request_uri` is visible in the browser's address bar!

The attacker:
1. Sees the authorize URL in their browser
2. Copies the `request_uri` parameter
3. Copies the `state` parameter
4. Submits both to the AttackerApi

### Step 3: Victim's Browser Uses Stolen request_uri

The victim's browser (compromised with malicious JavaScript):

1. Polls AttackerApi and receives the stolen `request_uri`
2. Creates a hidden iframe with:
   ```
   https://localhost:5001/connect/authorize
     ?client_id=dpop
     &request_uri=urn:ietf:params:oauth:request_uri:6esc_11ACC5bwc014ltc14eY22c
     &response_mode=fragment    ← Forces code into URL fragment!
   ```

3. Authorization Server:
   - Looks up the `request_uri`
   - Retrieves the stored parameters (attacker's dpop_jkt, code_challenge, nonce)
   - Victim is already authenticated
   - Issues authorization code **bound to attacker's DPoP key**

4. Redirects to:
   ```
   https://localhost:5010/signin-oidc#code=ABC123&state=...
   ```

5. Malicious JavaScript:
   - Reads the code from the URL fragment
   - Exfiltrates it to AttackerApi
   - Server never sees the code (fragments aren't sent to servers)

### Step 4: Attacker Completes Token Exchange

1. Attacker retrieves stolen code from AttackerApi
2. Navigates to: `/signin-oidc?code=ABC123&state=<ATTACKER_STATE>`
3. WebClient backend:
   - Validates state (matches attacker's correlation cookie) ✅
   - Exchanges code for tokens using:
     - Stolen authorization code
     - Attacker's `code_verifier` (from correlation cookie)
     - Attacker's DPoP key (matches the `dpop_jkt` in PAR)
   - Validates ID token nonce (matches attacker's nonce) ✅
4. **Attack succeeds!** Attacker is authenticated as the victim

## Why PAR Fails to Prevent This

### Problem 1: request_uri is Exposed in Front-Channel

```
WITHOUT PAR:
Browser sees: /authorize?dpop_jkt=ATTACKER&code_challenge=ATTACKER&nonce=ATTACKER
              ↑ All parameters visible and stealable

WITH PAR:
Browser sees: /authorize?request_uri=urn:...abc123&state=ATTACKER_STATE
              ↑ request_uri is visible and stealable!
```

**The `request_uri` is just an opaque reference**, but:
- It's transmitted in the front-channel (visible to attacker)
- It can be copied and given to the victim's browser
- It contains all the attacker's parameters (dpop_jkt, code_challenge, nonce)

### Problem 2: request_uri is Reusable

The `request_uri` can be used by **any browser** that has access to it:

```
Attacker's Browser:
  /authorize?request_uri=urn:...abc123
  → AS looks up parameters
  → Parameters bound to ATTACKER

Victim's Browser (using same request_uri):
  /authorize?request_uri=urn:...abc123
  → AS looks up SAME parameters
  → Parameters STILL bound to ATTACKER
  → Code issued bound to ATTACKER's key
```

**There's no binding between the `request_uri` and the browser/session that will use it.**

### Problem 3: response_mode=fragment Bypasses Server

Even with PAR, the attacker can force `response_mode=fragment`:

```
Victim's iframe:
  /authorize?request_uri=urn:...abc123&response_mode=fragment
  
Authorization Server returns:
  /signin-oidc#code=ABC123    ← Code in fragment, not query!
  
Server-side OIDC middleware:
  Receives: /signin-oidc (no code!)
  → Cannot process the callback
  → 500 error (expected)
  
Malicious JavaScript:
  Reads: window.location.hash = "#code=ABC123"
  → Steals the code
  → Exfiltrates to attacker
```

The code never reaches the legitimate OAuth flow!

## What PAR DOES Protect Against

PAR is effective against attacks where:

1. **Attacker cannot initiate the legitimate OAuth flow**
   - If attacker has no access to the client application
   - If attacker cannot trigger the PAR request

2. **Attacker cannot see the request_uri**
   - If the authorize redirect happens server-side only
   - If the browser never sees the request_uri

3. **Attacker cannot make their own PAR requests**
   - Confidential clients (requires client_secret)
   - CORS-protected PAR endpoints
   - Rate-limited PAR endpoints

## What PAR Does NOT Protect Against

PAR fails when:

1. ❌ **Attacker can initiate legitimate OAuth flow** (this demo)
2. ❌ **request_uri is visible in browser** (front-channel exposure)
3. ❌ **request_uri can be reused** (no session binding)
4. ❌ **Malicious JavaScript can manipulate authorization request** (add response_mode=fragment)
5. ❌ **Malicious JavaScript can intercept authorization code** (from fragment)

## The Fundamental Problem

```
┌─────────────────────────────────────────────────────────────┐
│ PAR protects the back-channel:                              │
│   Client Backend ←──(secure)──→ Authorization Server        │
│                                                              │
│ PAR does NOT protect the front-channel:                     │
│   Browser ←──(visible)──→ Authorization Server              │
│   ↑                                                          │
│   └─ request_uri is exposed here!                           │
│   └─ Malicious JS can see and manipulate it!                │
└─────────────────────────────────────────────────────────────┘
```

**Key insight**: PAR moves parameter negotiation to the back-channel, but the `request_uri` itself is still transmitted in the front-channel where it can be stolen and reused.

## Comparison: With and Without PAR

### Without PAR (Original Attack)

```
Attacker sees:
  /authorize?dpop_jkt=ATT&code_challenge=ATT&nonce=ATT&state=ATT

Victim uses:
  /authorize?dpop_jkt=ATT&code_challenge=ATT&nonce=ATT&state=VIC
  
Problem: Victim can construct new request with attacker's crypto params
```

### With PAR (This Demo)

```
Attacker sees:
  /authorize?request_uri=urn:...abc123&state=ATT

Victim uses:
  /authorize?request_uri=urn:...abc123&state=VIC&response_mode=fragment
  
Problem: Victim reuses attacker's request_uri (which contains attacker's crypto params)
```

**Both attacks succeed!** PAR doesn't prevent the reuse of the attacker's cryptographic parameters.

## Effective Mitigations

### 1. Prevent JavaScript Compromise (Primary)

- **Content Security Policy (CSP)**: Block malicious scripts
- **Subresource Integrity (SRI)**: Verify script integrity
- **Regular security audits**: Review all JavaScript dependencies

### 2. Browser-Bound request_uri (Not in Current Specs)

Hypothetical improvement to PAR:
```
POST /par
...
Browser-Session-ID: <cryptographic_binding>

Returns:
request_uri=urn:...abc123
  ↑ Bound to specific browser session
  
/authorize?request_uri=urn:...abc123
  ↑ AS validates Browser-Session-ID matches
```

This would prevent request_uri reuse across browsers.

### 3. Disable response_mode Override

Authorization Server could:
- Ignore `response_mode` parameter in authorize request when using PAR
- Always use the `response_mode` specified in the PAR request
- Prevent clients from overriding it in the front-channel

### 4. Short-Lived request_uri

- Very short expiration (e.g., 10-30 seconds)
- Reduces attack window
- Doesn't prevent attack, just makes it harder

### 5. Single-Use request_uri

- Mark request_uri as used after first authorization request
- Prevent reuse by victim's browser
- May break legitimate retry scenarios

## Conclusion

**PAR is a valuable security improvement** that:
- ✅ Protects against many parameter injection attacks
- ✅ Moves sensitive parameters out of browser history/logs
- ✅ Prevents modification of parameters in transit

**But PAR alone does NOT prevent this attack** because:
- ❌ The `request_uri` is still exposed in the front-channel
- ❌ The `request_uri` can be reused by a different browser
- ❌ Malicious JavaScript can still manipulate the authorization request
- ❌ The authorization code can still be intercepted using `response_mode=fragment`

**The root cause is JavaScript compromise**, and the only complete solution is to prevent malicious JavaScript from executing in the first place (CSP, SRI, secure coding practices).

## Testing This Demo

1. **Start all services** (IdentityServerHost, WebClient, Api, AttackerApi)
2. **Attacker browser**: Navigate to `/Home/AttackDemo`
3. **Start OAuth flow**: Copy the `request_uri` from the authorize URL
4. **Submit request_uri**: Paste it into the form
5. **Victim browser**: Navigate to `/Home/Secure` and log in as `alice`
6. **Observe**: The malicious JavaScript steals the code using the stolen `request_uri`
7. **Attacker completes**: Uses the stolen code to authenticate as Alice

The attack succeeds even with PAR enabled! 🎯

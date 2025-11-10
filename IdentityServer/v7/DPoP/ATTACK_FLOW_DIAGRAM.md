# DPoP Attack Flow - Visual Diagram

## Attack Sequence Diagram

```
┌─────────────┐         ┌─────────────┐         ┌─────────────┐         ┌─────────────┐
│  Attacker   │         │ AttackerApi │         │   Victim    │         │IdentityServer│
│  Browser    │         │  (Port 7666)│         │  Browser    │         │  (Port 5001) │
└──────┬──────┘         └──────┬──────┘         └──────┬──────┘         └──────┬──────┘
       │                       │                       │                       │
       │                       │                       │                       │
       │ PHASE 1: Capture OAuth Parameters            │                       │
       │────────────────────────────────────────────────────────────────────────────────
       │                       │                       │                       │
       │ 1. Start OAuth Flow   │                       │                       │
       │───────────────────────────────────────────────────────────────────────>│
       │                       │                       │                       │
       │ 2. Redirect to /authorize with:               │                       │
       │    - dpop_jkt (attacker's DPoP key thumbprint)│                       │
       │    - state (attacker's state)                 │                       │
       │    - code_challenge (attacker's PKCE)         │                       │
       │    - nonce (attacker's nonce)                 │                       │
       │<──────────────────────────────────────────────────────────────────────│
       │                       │                       │                       │
       │ 3. Copy parameters    │                       │                       │
       │    from URL           │                       │                       │
       │                       │                       │                       │
       │                       │                       │                       │
       │ PHASE 2: Send Parameters to Victim           │                       │
       │────────────────────────────────────────────────────────────────────────────────
       │                       │                       │                       │
       │ 4. POST /api/attack/dpop                      │                       │
       │    {dpop_jkt, state,  │                       │                       │
       │     code_challenge,   │                       │                       │
       │     nonce}            │                       │                       │
       │──────────────────────>│                       │                       │
       │                       │                       │                       │
       │ 5. Store parameters   │                       │                       │
       │    (in memory)        │                       │                       │
       │                       │                       │                       │
       │                       │                       │                       │
       │ PHASE 3: Victim Receives Stolen Parameters   │                       │
       │────────────────────────────────────────────────────────────────────────────────
       │                       │                       │                       │
       │                       │ 6. Poll for parameters│                       │
       │                       │    GET /api/attack/dpop/latest                │
       │                       │<──────────────────────│                       │
       │                       │                       │                       │
       │                       │ 7. Return stolen params                       │
       │                       │    {dpop_jkt, state,  │                       │
       │                       │     code_challenge,   │                       │
       │                       │     nonce}            │                       │
       │                       │──────────────────────>│                       │
       │                       │                       │                       │
       │                       │                       │                       │
       │ PHASE 4: Victim Makes Silent Authorization   │                       │
       │────────────────────────────────────────────────────────────────────────────────
       │                       │                       │                       │
       │                       │                       │ 8. Victim clicks      │
       │                       │                       │    "Authenticate"     │
       │                       │                       │                       │
       │                       │                       │ 9. Silent auth request│
       │                       │                       │    with stolen params:│
       │                       │                       │    - dpop_jkt (attacker's)
       │                       │                       │    - code_challenge (attacker's)
       │                       │                       │    - nonce (attacker's)
       │                       │                       │──────────────────────>│
       │                       │                       │                       │
       │                       │                       │ 10. Victim has active │
       │                       │                       │     session - no login│
       │                       │                       │     required          │
       │                       │                       │                       │
       │                       │                       │ 11. Authorization code│
       │                       │                       │     bound to attacker's
       │                       │                       │     dpop_jkt          │
       │                       │                       │<──────────────────────│
       │                       │                       │                       │
       │                       │                       │                       │
       │ PHASE 5: Victim Intercepts and Exfiltrates Code                      │
       │────────────────────────────────────────────────────────────────────────────────
       │                       │                       │                       │
       │                       │                       │ 12. Malicious JS      │
       │                       │                       │     intercepts code   │
       │                       │                       │                       │
       │                       │ 13. POST /api/attack/code                     │
       │                       │    {code, state}      │                       │
       │                       │<──────────────────────│                       │
       │                       │                       │                       │
       │                       │ 14. Store stolen code │                       │
       │                       │                       │                       │
       │                       │                       │                       │
       │ PHASE 6: Attacker Completes Token Exchange   │                       │
       │────────────────────────────────────────────────────────────────────────────────
       │                       │                       │                       │
       │ 15. GET /api/attack/code/{sessionId}          │                       │
       │──────────────────────>│                       │                       │
       │                       │                       │                       │
       │ 16. Return stolen code│                       │                       │
       │<──────────────────────│                       │                       │
       │                       │                       │                       │
       │ 17. Inject code into  │                       │                       │
       │     OAuth callback    │                       │                       │
       │     /signin-oidc?code=STOLEN&state=ATTACKER   │                       │
       │                       │                       │                       │
       │ 18. Exchange code for tokens                  │                       │
       │    POST /connect/token                        │                       │
       │    - code (stolen)    │                       │                       │
       │    - code_verifier (attacker's - from cookie) │                       │
       │    - DPoP proof (attacker's key)              │                       │
       │───────────────────────────────────────────────────────────────────────>│
       │                       │                       │                       │
       │ 19. Validate:         │                       │                       │
       │     ✓ code is valid   │                       │                       │
       │     ✓ code_verifier matches code_challenge    │                       │
       │     ✓ DPoP key matches dpop_jkt               │                       │
       │     ✓ nonce in ID token matches               │                       │
       │                       │                       │                       │
       │ 20. Return tokens     │                       │                       │
       │     (bound to attacker's DPoP key)            │                       │
       │<──────────────────────────────────────────────────────────────────────│
       │                       │                       │                       │
       │ 21. ✅ ATTACK SUCCESS │                       │                       │
       │     Attacker is now   │                       │                       │
       │     authenticated as  │                       │                       │
       │     the victim!       │                       │                       │
       │                       │                       │                       │
```

## Key Components and Their Roles

### 1. Attacker Browser
- **Purpose**: Initiates legitimate OAuth flow to capture parameters
- **Key Actions**:
  - Generates DPoP key pair
  - Starts OAuth flow (triggers PKCE generation)
  - Captures `dpop_jkt`, `state`, `code_challenge`, `nonce` from URL
  - Submits parameters to AttackerApi
  - Retrieves stolen authorization code
  - Completes token exchange

### 2. AttackerApi (Port 7666)
- **Purpose**: Coordination server for the attack
- **Key Actions**:
  - Stores stolen OAuth parameters from attacker
  - Provides parameters to victim's browser via polling endpoint
  - Receives stolen authorization code from victim
  - Returns stolen code to attacker
- **Endpoints**:
  - `POST /api/attack/dpop` - Store OAuth parameters
  - `GET /api/attack/dpop/latest` - Retrieve latest parameters (for victim)
  - `POST /api/attack/code` - Store stolen authorization code
  - `GET /api/attack/code/{sessionId}` - Retrieve stolen code (for attacker)

### 3. Victim Browser (Compromised)
- **Purpose**: Executes the authorization request with stolen parameters
- **Key Actions**:
  - Has active session with IdentityServer (already logged in)
  - Polls AttackerApi for stolen OAuth parameters
  - Makes silent authorization request using attacker's parameters
  - Intercepts authorization code via malicious JavaScript
  - Exfiltrates code to AttackerApi
- **Malicious Script**: `https://localhost:7666/attack.js`

### 4. IdentityServer (Port 5001)
- **Purpose**: OAuth/OIDC Authorization Server
- **Key Actions**:
  - Issues authorization code bound to `dpop_jkt` from request
  - Validates PKCE `code_challenge`
  - Validates DPoP proof during token exchange
  - Issues tokens bound to attacker's DPoP key
- **Cannot Detect**: That the authorization request came from victim's browser with attacker's parameters

## Critical Security Bindings

### What Gets Bound to the Attacker

| Parameter | Generated By | Stored Where | Used When | Validates |
|-----------|-------------|--------------|-----------|-----------|
| `dpop_jkt` | Attacker's DPoP key | Authorization request URL | Token exchange | DPoP proof matches key |
| `code_challenge` | Attacker's PKCE verifier | Authorization request URL | Token exchange | code_verifier matches |
| `code_verifier` | Attacker's browser (ASP.NET) | Attacker's correlation cookie | Token exchange | Matches code_challenge |
| `nonce` | Attacker's browser (ASP.NET) | Authorization request URL & ID token | ID token validation | Prevents replay |
| `state` | Attacker's browser (ASP.NET) | Attacker's correlation cookie | Callback validation | CSRF protection |

### Why All Validations Pass

1. **DPoP Validation**: ✅ 
   - Authorization code was issued with attacker's `dpop_jkt`
   - Token exchange uses attacker's DPoP key
   - Thumbprints match

2. **PKCE Validation**: ✅
   - Victim used attacker's `code_challenge` in authorization request
   - Attacker uses matching `code_verifier` in token exchange
   - SHA256(code_verifier) == code_challenge

3. **Nonce Validation**: ✅
   - Victim used attacker's `nonce` in authorization request
   - ID token contains the same nonce
   - Attacker's browser expects this nonce

4. **State Validation**: ✅
   - Attacker's correlation cookie contains the state
   - Callback includes the same state
   - CSRF protection passes

## Why This Attack Works

### The Fundamental Problem

**DPoP binds tokens to a key, but doesn't verify WHO initiated the authorization request.**

The authorization server sees:
- A valid authorization request with a `dpop_jkt`
- A valid token exchange with matching DPoP proof
- All cryptographic bindings are correct

The authorization server **cannot know** that:
- The authorization request came from a different browser than the token exchange
- The user in the authorization request (victim) is different from the token recipient (attacker)
- Malicious JavaScript coordinated the attack

### The Attack Vector

The attack exploits the **separation between front-channel (authorization) and back-channel (token exchange)**:

1. **Front-channel** (victim's browser): Makes authorization request with attacker's parameters
2. **Back-channel** (attacker's browser): Exchanges code with attacker's credentials

The authorization server treats these as a single, legitimate flow because all the cryptographic bindings match.

## Defense: Pushed Authorization Requests (PAR)

PAR prevents this attack by:

1. **Back-channel parameter submission**: Client pushes parameters to AS via authenticated back-channel
2. **Request URI**: AS returns a one-time-use request URI
3. **Front-channel reference**: Authorization request only includes the request URI
4. **Malicious JS cannot modify**: Parameters are locked in before front-channel redirect

With PAR, the victim's browser cannot inject attacker's parameters because they're already committed on the back-channel.

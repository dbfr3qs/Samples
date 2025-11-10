# Quick Start: PAR Attack Demonstration

## What This Demonstrates

This demo proves that **Pushed Authorization Requests (PAR) does NOT prevent the browser swapping attack** when:
- The `request_uri` is visible in the front-channel
- Malicious JavaScript can intercept the authorization code using `response_mode=fragment`

## Prerequisites

- .NET 10.0 SDK
- Two browser windows (or different browser profiles)

## Quick Start Steps

### 1. Start All Services

```bash
# Use the convenience script
./start-all.sh

# Or manually in 4 terminals:
cd IdentityServerHost && dotnet run
cd Api && dotnet run
cd WebClient && dotnet run
cd AttackerApi && dotnet run
```

Wait for all services to start:
- IdentityServerHost: `https://localhost:5001`
- Api: `https://localhost:5005`
- WebClient: `https://localhost:5010`
- AttackerApi: `https://localhost:7666`

### 2. Attacker Browser Setup

1. **Open Browser 1** (e.g., Chrome)
2. Navigate to: `https://localhost:5010/Home/AttackDemo`
3. Click **"Initialize Attack"**
4. Click **"Start OAuth Flow"**
5. **You'll be redirected to the authorize endpoint**

### 3. Capture the request_uri

⚠️ **This is the key step!**

Look at the URL in your browser's address bar. It should look like:
```
https://localhost:5001/connect/authorize
  ?client_id=dpop
  &request_uri=urn:ietf:params:oauth:request_uri:6esc_11ACC5bwc014ltc14eY22c
  &state=CfDJ8...
```

**Copy two things:**
1. The `request_uri` parameter (e.g., `urn:ietf:params:oauth:request_uri:...`)
2. The `state` parameter

### 4. Submit to AttackerApi

1. **Go back** to the AttackDemo page (browser back button)
2. **Paste** the `request_uri` into the first input field
3. **Paste** the `state` into the second input field
4. Click **"Submit request_uri"**
5. You should see: ✓ Successfully submitted request_uri!

### 5. Victim Browser Setup

1. **Open Browser 2** (e.g., Firefox or Chrome Incognito)
2. Navigate to: `https://localhost:5010/Home/Secure`
3. **Log in as the victim**: `alice` / `alice`
4. You should see the Secure page with claims

**Keep this browser logged in!**

### 6. Observe the Attack

The malicious JavaScript on the `/Home/Secure` page is now:
1. Polling the AttackerApi for a stolen `request_uri`
2. It will find the one you submitted!
3. It creates a hidden iframe with:
   ```
   /authorize?request_uri=<STOLEN>&response_mode=fragment
   ```
4. The authorization code is returned in the URL fragment
5. JavaScript steals it and sends it to AttackerApi

**Check the browser console** (F12) to see:
```
[MALICIOUS JS] Found request_uri from attacker!
[MALICIOUS JS] Starting silent OAuth flow with stolen request_uri (PAR)...
[MALICIOUS JS] ✅ Successfully sent authorization code to attacker!
```

### 7. Complete the Attack

1. **Return to Browser 1** (attacker)
2. Click **"Check Status"** - you should see the stolen code
3. Click **"Complete Login with Stolen Code"**
4. **You're now logged in as Alice!** 🎯

Check the `/Home/Secure` page - you'll see Alice's claims even though you're in the attacker's browser!

## What Just Happened?

### The Attack Flow

```
1. Attacker initiates OAuth → WebClient makes PAR request
   ↓
2. PAR returns request_uri (contains attacker's dpop_jkt, code_challenge, nonce)
   ↓
3. Attacker copies request_uri from browser URL
   ↓
4. Victim's browser uses stolen request_uri
   ↓
5. Authorization code issued (bound to attacker's DPoP key!)
   ↓
6. Malicious JS intercepts code from fragment
   ↓
7. Attacker uses stolen code with their own session
   ↓
8. ✅ Attacker authenticated as victim!
```

### Why PAR Didn't Help

- ✅ PAR moved parameters to back-channel (secure)
- ❌ But `request_uri` is still in front-channel (visible!)
- ❌ `request_uri` can be reused by victim's browser
- ❌ `request_uri` contains attacker's cryptographic parameters
- ❌ Malicious JavaScript can still intercept the code

## Key Observations

### In the WebClient Console

You should see logs like:
```
[Warning] ⚠️ PAR request_uri exposed in front-channel: urn:ietf:params:oauth:request_uri:...
[Warning] ⚠️ This request_uri can be stolen and reused by an attacker!
```

### In the Attacker Browser

The AttackDemo page shows:
```
✓ Successfully submitted request_uri!

The victim can now use this request_uri to authorize with YOUR parameters 
(dpop_jkt, code_challenge, nonce).
```

### In the Victim Browser Console

```
[MALICIOUS JS] Found request_uri from attacker! Session: session_abc123
[MALICIOUS JS] Stolen request_uri: urn:ietf:params:oauth:request_uri:...
[MALICIOUS JS] ⚠️ This request_uri contains the attacker's dpop_jkt, code_challenge, and nonce!
[MALICIOUS JS] Starting silent OAuth flow with stolen request_uri (PAR)...
[MALICIOUS JS] ✅ Successfully sent authorization code to attacker!
```

## Troubleshooting

### "request_uri not found" Error

- Make sure you submitted the `request_uri` in step 4
- Check that the AttackerApi is running on port 7666
- Verify CORS is working (check browser console for errors)

### "No stolen code available"

- Make sure the victim browser is logged in first (step 5)
- Check that the malicious JavaScript is running (look for console logs)
- Verify the victim browser visited `/Home/Secure` after you submitted the request_uri

### PAR Not Working

- Verify `RequirePushedAuthorization = true` in `IdentityServerHost/Clients.cs`
- Verify `PushedAuthorizationBehavior = Require` in `WebClient/Program.cs`
- Restart both services after making changes

### Certificate Errors

Accept self-signed certificates for all services:
- `https://localhost:5001` (IdentityServerHost)
- `https://localhost:5005` (Api)
- `https://localhost:5010` (WebClient)
- `https://localhost:7666` (AttackerApi)

## Understanding the Logs

### WebClient Logs (Terminal)

```
[Information] === AUTHORIZATION REQUEST ===
[Information] Full URL: https://localhost:5001/connect/authorize?client_id=dpop&request_uri=urn:...
[Warning] ⚠️ PAR request_uri exposed in front-channel: urn:ietf:params:oauth:request_uri:...
```

This shows the `request_uri` is visible!

### AttackerApi Logs (Terminal)

```
[ATTACKER] Stored PAR request_uri for session: session_abc123
[ATTACKER] Stored auth code for session: session_abc123
```

This shows the stolen data flowing through the attack infrastructure.

## Next Steps

- Read [PAR_ATTACK_ANALYSIS.md](PAR_ATTACK_ANALYSIS.md) for detailed technical analysis
- Read [ATTACK_DEMO_README.md](ATTACK_DEMO_README.md) for the original attack without PAR
- Experiment with different mitigations (CSP, SRI, etc.)

## Key Takeaway

**PAR is a valuable security improvement**, but it's **not a silver bullet**. When the `request_uri` is exposed in the front-channel and can be reused, the attack still succeeds.

**The only complete solution is preventing JavaScript compromise** through CSP, SRI, and secure coding practices.

# Quick Start Guide - DPoP Browser Swapping Attack Demo

## What Was Fixed

The following build errors have been resolved:

1. **HomeController.cs** - Fixed duplicate closing braces and moved `AttackDemo` and `AttackVictim` methods inside the class
2. **AttackerApi/Program.cs** - Removed extra closing brace at end of file
3. **attack.js** - Removed duplicate code in `interceptAuthCallback` method and XHR send interceptor

## Quick Start

### Option 1: Automated Start (macOS)

```bash
./start-all.sh
```

This will open 4 terminal tabs with all services running.

### Option 2: Manual Start

Open 4 terminal windows:

```bash
# Terminal 1
cd IdentityServerHost && dotnet run

# Terminal 2
cd Api && dotnet run

# Terminal 3
cd WebClient && dotnet run

# Terminal 4
cd AttackerApi && dotnet run
```

## Running the Attack

### 1. Setup Attacker Browser

1. Open Chrome (or your primary browser)
2. Navigate to: `https://localhost:5010/Home/AttackDemo`
3. Accept any certificate warnings
4. Click **"Initialize Attack"**
5. **Copy the Session ID** that appears (e.g., `session_abc123xyz`)
6. Click **"Start OAuth Flow"**
7. Login with: `alice` / `alice` (or `bob` / `bob`)
8. The DPoP proof will be intercepted and sent to AttackerApi

### 2. Setup Victim Browser

1. Open a **different browser or incognito window** (e.g., Firefox or Chrome Incognito)
2. **First, establish a session with IdentityServer:**
   - Go to: `https://localhost:5010/Home/Secure`
   - Login with: `alice` / `alice`
   - You should see the secure page
3. **Now navigate to:** `https://localhost:5010/Home/AttackVictim`
4. **Paste the Session ID** from step 1 into the input field
5. Click **"Start Victim Simulation"**
6. Wait for "DPoP Proof Received!" status
7. Click **"Manually Authenticate"**
8. The authorization code will be intercepted and sent to AttackerApi

### 3. Complete the Attack

1. Return to the **Attacker browser**
2. Click **"Check Status"** - you should see the stolen code
3. Click **"Complete Login with Stolen Code"**
4. The attacker is now logged in as the victim!

## Key Points

### Why the Victim Needs an Existing Session

The attack works because:
- The victim is already logged into IdentityServer
- When the OAuth flow starts, IdentityServer recognizes the existing session
- The authorization code is issued without requiring re-authentication
- This simulates a real-world scenario where users stay logged in

### Session ID Coordination

- Both browsers must use the **same Session ID**
- Copy the Session ID from the attacker browser
- Paste it into the victim browser's input field
- This coordinates the attack through the AttackerApi

### Browser Separation

- Use different browsers or profiles to simulate attacker vs victim
- This ensures separate cookie stores and sessions
- The victim's IdentityServer session is what gets exploited

## Troubleshooting

### "DPoP Proof Not Found"

- Make sure you completed the attacker's OAuth flow first
- Check that the AttackerApi is running on port 7666
- Verify the Session ID matches exactly

### "Authorization Code Not Intercepted"

- Ensure the victim has an active session with IdentityServer
- Check that you're using the victim browser (not attacker browser)
- Look for JavaScript errors in the browser console

### Certificate Errors

Accept the self-signed certificates for all services:
- https://localhost:5001 (IdentityServer)
- https://localhost:5005 (Api)
- https://localhost:5010 (WebClient)
- https://localhost:7666 (AttackerApi)

### CORS Errors

- Ensure AttackerApi is running
- Check browser console for specific CORS errors
- AttackerApi has CORS enabled by default

## Architecture

```
┌─────────────────┐
│ IdentityServer  │ :5001 - Authorization Server
└─────────────────┘
         │
         │ OAuth Flow
         │
┌─────────────────┐
│   WebClient     │ :5010 - Target Application
└─────────────────┘
         │
         │ Protected API Calls
         │
┌─────────────────┐
│      Api        │ :5005 - Protected Resource
└─────────────────┘

┌─────────────────┐
│  AttackerApi    │ :7666 - Coordinates Attack
└─────────────────┘
         │
         ├─ Stores stolen DPoP proofs
         └─ Stores stolen auth codes
```

## What's Happening

1. **Attacker's DPoP Proof** is intercepted by malicious JavaScript and sent to AttackerApi
2. **Victim's Browser** polls AttackerApi and receives the stolen DPoP proof
3. **Victim Authenticates** using their existing IdentityServer session
4. **Authorization Code** is intercepted before completing the OAuth flow
5. **Attacker Retrieves** the stolen code and completes their login

## Next Steps

See `ATTACK_DEMO_README.md` for:
- Detailed attack flow explanation
- Security implications
- Mitigation strategies
- Educational context

## Important Notice

⚠️ **This is for educational and security research purposes only.**

Do not use these techniques against systems you don't own or have explicit permission to test.

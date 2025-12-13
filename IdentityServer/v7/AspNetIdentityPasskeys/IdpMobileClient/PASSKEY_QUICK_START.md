# Passkey Authentication Quick Start

## Prerequisites

1. **Server running** at `https://idp.dev.internal:5001`
2. **Certificate trusted** on your iOS device/simulator
3. **Mobile app built and deployed**

## Step-by-Step Guide

### 1. Register a Passkey

```
1. Launch the mobile app
2. Tap "Register Passkey"
3. Enter:
   - Username: testuser
   - Email: test@example.com
4. Tap "Register"
5. Complete Face ID/Touch ID prompt
6. Wait for success message
```

**Expected Logs:**
```
🔐 [Passkey] Requesting registration options from IdP...
🌐 [PasskeyService] POST https://idp.dev.internal:5001/api/passkey/register/begin
📡 [PasskeyService] Response status: 200
✅ [Passkey] Received challenge: ...
🔐 [Passkey] Creating registration request...
🔐 [Passkey] Presenting passkey registration prompt...
✅ [Passkey] Registration credential received
✅ [Passkey] Registration completed successfully!
```

### 2. Authenticate with Passkey

```
1. Tap "Sign in with Passkey"
2. Complete Face ID/Touch ID prompt
3. Wait for authentication to complete
```

**Expected Logs:**
```
🔐 [Passkey] Requesting authentication options from IdP...
🌐 [PasskeyService] POST https://idp.dev.internal:5001/api/passkey/authenticate/begin
📡 [PasskeyService] Response status: 200
✅ [PasskeyService] Successfully decoded authentication options
✅ [Passkey] Received challenge: ...
🔐 [Passkey] Creating authentication request...
✅ [PasskeyService] Successfully decoded challenge, length: 32 bytes
🔑 [PasskeyService] Using rpId: idp.dev.internal
🔐 [Passkey] Presenting passkey prompt...
✅ [Passkey] Authentication credential received
```

## Common Errors

### Error 1004: No Passkeys Found

**Symptom:**
```
ASAuthorizationController credential request failed with error: 
Error Domain=com.apple.AuthenticationServices.AuthorizationError Code=1004 "(null)"
```

**Cause:** No passkey registered for `idp.dev.internal` on this device.

**Solution:** Complete the registration flow first (see Step 1 above).

### Error: No challenge found in session

**Symptom:**
```
{"error": "No challenge found in session"}
```

**Cause:** Session cookies not being sent between `begin` and `complete` requests.

**Solution:** 
- Ensure URLSession is handling cookies (it should by default)
- Check that both requests are going to the same server
- Verify session middleware is enabled on the server

### Error: Authentication failed

**Symptom:**
```
{"error": "Passkey authentication failed", "detail": "..."}
```

**Possible Causes:**
- RP ID mismatch
- Challenge validation failed
- Passkey not found in database
- User account deleted

**Solution:** Check server logs for detailed error messages.

## Verification

### Check if Passkey is Registered

**On iOS Device:**
1. Settings > Passwords
2. Search for "idp.dev.internal"
3. You should see a passkey entry

**On iOS Simulator:**
Passkeys are stored in the simulator's keychain and persist across app launches.

### Check Server Database

Query the database to verify the passkey was stored:
```sql
SELECT * FROM AspNetUserPasskeys WHERE UserId = '<user-id>';
```

## Architecture

### Registration Flow
```
Mobile App → POST /api/passkey/register/begin
         ← Registration Options (challenge, user info)
         
Mobile App → iOS Passkey Registration Prompt
         ← Credential (public key, attestation)
         
Mobile App → POST /api/passkey/register/complete
         ← Success (passkey stored in database)
```

### Authentication Flow
```
Mobile App → POST /api/passkey/authenticate/begin
         ← Authentication Options (challenge)
         
Mobile App → iOS Passkey Authentication Prompt
         ← Credential (signature, authenticator data)
         
Mobile App → POST /api/passkey/authenticate/complete
         ← Authorization Code
         
Mobile App → POST /connect/token (with auth code)
         ← Access Token, ID Token, Refresh Token
```

## Endpoints

| Endpoint | Method | Purpose |
|----------|--------|---------|
| `/api/passkey/register/begin` | POST | Get registration options |
| `/api/passkey/register/complete` | POST | Complete registration |
| `/api/passkey/authenticate/begin` | POST | Get authentication options |
| `/api/passkey/authenticate/complete` | POST | Complete authentication |
| `/connect/token` | POST | Exchange code for tokens |

## Configuration

### Client (PasskeyAuthService.swift)
```swift
idpBaseURL: "https://idp.dev.internal:5001"
relyingPartyIdentifier: "idp.dev.internal"
```

### Server (HostingExtensions.cs)
```csharp
options.ValidateOrigin = context => ValueTask.FromResult(
    context.Origin == "https://idp.dev.internal:5001");
```

## Next Steps

After successful authentication:
1. Access token is stored in the app
2. Use it to call protected APIs
3. Refresh token can be used to get new access tokens
4. Sign out clears all stored tokens

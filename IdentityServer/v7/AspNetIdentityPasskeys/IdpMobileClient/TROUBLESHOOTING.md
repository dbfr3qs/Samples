# Passkey Authentication Troubleshooting

## Error 1004: "The operation failed because the request data is invalid"

### Root Cause
ASAuthorizationController error code 1004 typically indicates one of the following:

1. **No passkeys registered** for the specified Relying Party ID (RP ID) on this device
2. **RP ID mismatch** between registration and authentication
3. **Invalid challenge data** (less common)

### Solution Steps

#### 1. Verify RP ID Configuration
Ensure the RP ID matches between client and server:

**Client (PasskeyAuthService.swift):**
```swift
relyingPartyIdentifier: String = "idp.dev.internal"
```

**Server (appsettings.json or HostingExtensions.cs):**
The server should be configured to use `idp.dev.internal` as the RP ID.

#### 2. Register a Passkey First
Before you can authenticate with a passkey, you must register one:

1. **Launch the mobile app**
2. **Tap "Register Passkey"** button
3. **Enter username and email**
4. **Complete the passkey registration** when prompted by iOS
5. **Then try "Sign in with Passkey"**

#### 3. Check Device/Simulator Passkeys
On iOS Simulator or device:
- Go to **Settings > Passwords > Password Options**
- Verify passkeys are enabled
- Check if a passkey exists for `idp.dev.internal`

#### 4. Clear Passkeys (if needed)
If you need to start fresh:
- **iOS Device:** Settings > Passwords > [find idp.dev.internal] > Delete
- **iOS Simulator:** Reset simulator or delete passkeys manually

### Session Cookie Issues

The mobile passkey endpoints use server-side sessions to store the challenge. This requires cookies to work properly.

**Verify cookies are being sent:**
```swift
// URLSession.shared automatically handles cookies by default
// Check if cookies are being stored:
if let cookies = HTTPCookieStorage.shared.cookies(for: url) {
    print("Cookies: \(cookies)")
}
```

**If cookies aren't working:**
The mobile endpoints may need to be modified to:
1. Return the challenge to the client
2. Have the client send it back in the complete request
3. Validate the challenge matches the one in the credential response

### Debug Logging

The app now includes enhanced error logging. Look for:

```
❌ [Passkey] Error details: ...
❌ [Passkey] Error code: 1004
❌ [Passkey] Error domain: com.apple.AuthenticationServices.AuthorizationError
```

### Common Issues

| Issue | Symptom | Solution |
|-------|---------|----------|
| No passkeys registered | Error 1004 | Register a passkey first |
| RP ID mismatch | Error 1004 | Verify RP ID matches between client/server |
| Session cookie not sent | "No challenge found in session" | Ensure URLSession handles cookies |
| Wrong endpoint called | 401/403 errors | Use `/api/passkey/*` not `/Identity/Account/*` |

### Testing Checklist

- [ ] Server is running on `https://idp.dev.internal:5001`
- [ ] Certificate is trusted on device/simulator
- [ ] RP ID is `idp.dev.internal` in both client and server
- [ ] Passkey has been registered for a test user
- [ ] Cookies are enabled in URLSession
- [ ] Using mobile endpoints (`/api/passkey/*`)

### Additional Resources

- [Apple WebAuthn Documentation](https://developer.apple.com/documentation/authenticationservices/public-private_key_authentication)
- [ASAuthorizationError Codes](https://developer.apple.com/documentation/authenticationservices/asauthorizationerror)

# Domain Association Setup for Passkeys

## The Problem

You're seeing this error:
```
Application with identifier GSSMQP88ZN.com.idp.mobile.demo is not associated with domain idp.dev.internal
```

This means iOS cannot verify that your app is allowed to use passkeys for the domain `idp.dev.internal`.

## Solution: Configure Domain Association

### Step 1: Add Entitlements to Xcode Project

1. **Open Xcode project:**
   ```bash
   open IdpMobileClient.xcodeproj
   ```

2. **Select the target:**
   - Click on the project in the navigator
   - Select the "IdpMobileDemoApp" target

3. **Add Associated Domains capability:**
   - Go to "Signing & Capabilities" tab
   - Click "+ Capability"
   - Search for and add "Associated Domains"

4. **Add the domain:**
   - Under Associated Domains, click "+"
   - Add: `webcredentials:idp.dev.internal`

5. **Link the entitlements file:**
   - In Build Settings, search for "Code Signing Entitlements"
   - Set it to: `IdpMobileDemoApp.entitlements`

The entitlements file has already been created at:
`IdpMobileClient/IdpMobileDemoApp.entitlements`

### Step 2: Verify Server Configuration

The Apple App Site Association (AASA) file has been created at:
```
IdentityServerAspNetIdentityPasskeys/wwwroot/.well-known/apple-app-site-association
```

**Verify it's accessible:**
```bash
curl -k https://idp.dev.internal:5001/.well-known/apple-app-site-association
```

**Expected response:**
```json
{
  "webcredentials": {
    "apps": [
      "GSSMQP88ZN.com.idp.mobile.demo"
    ]
  }
}
```

### Step 3: Restart the Server

After adding the AASA file, restart the IdentityServer:

```bash
cd IdentityServerAspNetIdentityPasskeys
dotnet run
```

### Step 4: Rebuild and Reinstall the App

1. **Clean build folder:**
   - In Xcode: Product > Clean Build Folder (⇧⌘K)

2. **Rebuild:**
   - Product > Build (⌘B)

3. **Run on simulator/device:**
   - Product > Run (⌘R)

### Step 5: Verify Domain Association

iOS will automatically verify the domain association when the app launches. You can check if it worked:

1. **Check device logs** (if using a physical device):
   ```bash
   xcrun devicectl device info logs --device <device-id> --predicate 'subsystem == "com.apple.AuthenticationServices"'
   ```

2. **Look for messages like:**
   ```
   Successfully verified domain association for idp.dev.internal
   ```

### Alternative: Use localhost (Development Only)

For development, you can bypass domain association by using `localhost`:

1. **Change RP ID to localhost:**
   
   In `PasskeyAuthService.swift`:
   ```swift
   relyingPartyIdentifier: String = "localhost"
   ```

2. **Update server configuration:**
   
   In `HostingExtensions.cs`:
   ```csharp
   options.ValidateOrigin = context => ValueTask.FromResult(
       context.Origin == "https://localhost:5001");
   ```

3. **Update AASA file:**
   ```json
   {
     "webcredentials": {
       "apps": [
         "GSSMQP88ZN.com.idp.mobile.demo"
       ]
     }
   }
   ```

4. **Update entitlements:**
   ```xml
   <string>webcredentials:localhost</string>
   ```

**Note:** This approach only works for development. Production apps must use proper domain association.

## Troubleshooting

### AASA file not accessible

**Check:**
```bash
curl -k -v https://idp.dev.internal:5001/.well-known/apple-app-site-association
```

**Should return:**
- Status: 200 OK
- Content-Type: application/json
- Body: The JSON content

**If not working:**
1. Verify the file exists in `wwwroot/.well-known/`
2. Check server logs for errors
3. Ensure static file middleware is configured

### Domain association still failing

**Clear iOS cache:**
1. Delete the app from simulator/device
2. Reset simulator: Device > Erase All Content and Settings
3. Reinstall the app

**Check certificate:**
The certificate for `idp.dev.internal` must be trusted on the device/simulator.

### Wrong Team ID

If you're using a different Apple Developer account:

1. **Find your Team ID:**
   - Go to https://developer.apple.com/account
   - Team ID is shown in the top right

2. **Update AASA file:**
   ```json
   {
     "webcredentials": {
       "apps": [
         "YOUR_TEAM_ID.com.idp.mobile.demo"
       ]
     }
   }
   ```

3. **Restart server and rebuild app**

## Verification Checklist

- [ ] Entitlements file exists and is linked in Xcode
- [ ] Associated Domains capability added in Xcode
- [ ] Domain `webcredentials:idp.dev.internal` is listed
- [ ] AASA file exists at `/.well-known/apple-app-site-association`
- [ ] AASA file is accessible via HTTPS
- [ ] AASA file contains correct Team ID and Bundle ID
- [ ] Server restarted after adding AASA file
- [ ] App rebuilt and reinstalled after adding entitlements
- [ ] Certificate for `idp.dev.internal` is trusted

## Next Steps

After completing these steps:

1. **Register a passkey:**
   - Launch the app
   - Tap "Register Passkey"
   - Enter username and email
   - Complete Face ID/Touch ID

2. **Authenticate:**
   - Tap "Sign in with Passkey"
   - Complete Face ID/Touch ID

The error should be resolved!

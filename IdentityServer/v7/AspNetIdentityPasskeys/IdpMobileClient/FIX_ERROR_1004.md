# Fix Error 1004: Domain Association Required

## The Error
```
Application with identifier GSSMQP88ZN.com.idp.mobile.demo is not associated with domain idp.dev.internal
```

## Quick Fix (3 Steps)

### 1. Add Entitlements to Xcode Project

Open the project in Xcode:
```bash
cd /Users/chris.keogh/dev/Samples/IdentityServer/v7/AspNetIdentityPasskeys/IdpMobileClient
open IdpMobileClient.xcodeproj
```

Then:
1. Select the **IdpMobileDemoApp** target
2. Go to **Signing & Capabilities** tab
3. Click **+ Capability**
4. Add **Associated Domains**
5. Click **+** under Associated Domains
6. Enter: `webcredentials:idp.dev.internal`
7. In **Build Settings**, search for "Code Signing Entitlements"
8. Set to: `IdpMobileDemoApp.entitlements`

### 2. Restart the IdentityServer

The AASA file has been created. Restart the server to serve it:

```bash
cd /Users/chris.keogh/dev/Samples/IdentityServer/v7/AspNetIdentityPasskeys/IdentityServerAspNetIdentityPasskeys

# Stop the current server (Ctrl+C if running)

# Start it again
dotnet run
```

**Verify the AASA file is accessible:**
```bash
curl -k https://idp.dev.internal:5001/.well-known/apple-app-site-association
```

**Expected output:**
```json
{
  "webcredentials": {
    "apps": [
      "GSSMQP88ZN.com.idp.mobile.demo"
    ]
  }
}
```

### 3. Rebuild and Run the App

In Xcode:
1. **Product > Clean Build Folder** (⇧⌘K)
2. **Product > Build** (⌘B)
3. **Product > Run** (⌘R)

## Test It

1. **Launch the app**
2. **Tap "Register Passkey"**
3. **Enter username and email**
4. **Complete Face ID/Touch ID**

The error should be gone!

## Files Created

✅ `IdpMobileDemoApp.entitlements` - Entitlements file for Xcode
✅ `wwwroot/.well-known/apple-app-site-association` - Domain association file
✅ `HostingExtensions.cs` - Updated to serve AASA file

## What This Does

**Associated Domains** tells iOS: "This app is allowed to use passkeys for idp.dev.internal"

**AASA File** tells iOS: "The domain idp.dev.internal confirms this app is allowed"

iOS verifies both match, then allows passkey operations.

## Troubleshooting

### AASA file returns empty
- **Restart the server** (it needs to pick up the new file)
- Check the file exists: `ls -la IdentityServerAspNetIdentityPasskeys/wwwroot/.well-known/`

### Still getting error 1004
- **Delete the app** from simulator/device
- **Clean build folder** in Xcode
- **Rebuild and reinstall**
- iOS caches domain associations, so a fresh install helps

### Different Team ID
If you're using a different Apple Developer account, update the AASA file with your Team ID:
```json
{
  "webcredentials": {
    "apps": [
      "YOUR_TEAM_ID.com.idp.mobile.demo"
    ]
  }
}
```

Find your Team ID at: https://developer.apple.com/account

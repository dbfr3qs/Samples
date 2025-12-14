# iOS App Startup Troubleshooting

## Problem
App is blanking out and taking a long time to start when running from Xcode to iPhone.

## Likely Causes

### 1. DNS Resolution Timeout (Most Likely)
The app may be trying to make network requests during initialization that are timing out because `api.dev.internal` isn't resolving.

**Symptoms:**
- App shows blank screen
- Takes 30-60+ seconds to start
- Eventually loads or crashes
- Xcode console shows network timeout errors

**Solution:**
Fix the DNS resolution first (see below).

### 2. Network Request on Main Thread
The app's `init()` method calls `checkAuthenticationStatus()` which might be blocking if there are network issues.

**Location:** `ContentView.swift:239`
```swift
override init() {
    super.init()
    self.apiClient = ApiClient(oauthClient: oauthClient)
    checkAuthenticationStatus()  // This runs on init
}
```

This should be fine since it only checks local token storage, but if DNS is broken, it might cause issues.

### 3. Certificate Validation Timeout
If the certificate isn't installed or trusted, SSL handshake might be timing out.

## Quick Fixes

### Fix 1: Clear iPhone DNS Cache (Do This First)
The iPhone can resolve `idp.dev.internal` but not `api.dev.internal` - this is a DNS cache issue.

**Toggle Airplane Mode:**
1. Settings → Airplane Mode → ON
2. Wait 5 seconds
3. Toggle OFF
4. Try running the app again

### Fix 2: Verify DNS Configuration
Settings → Wi-Fi → (i) → DNS should show: `192.168.178.153`

If not:
1. Configure DNS → Manual
2. Remove all servers
3. Add: `192.168.178.153`
4. Save

### Fix 3: Test DNS Before Running App
Open Safari on iPhone:
```
http://api.dev.internal:5002
```

**Expected:** Connection error (needs HTTPS) - this means DNS works!
**Bad:** "Cannot Find Server" - DNS still broken

### Fix 4: Clean Build
In Xcode:
1. Product → Clean Build Folder (Shift+Cmd+K)
2. Delete app from iPhone
3. Rebuild and run

### Fix 5: Check Xcode Console
Look for these errors in Xcode console:
- `NSURLErrorDomain Code=-1003` - DNS lookup failed
- `NSURLErrorDomain Code=-1001` - Request timed out
- `NSURLErrorDomain Code=-1200` - SSL error

## Debugging Steps

### Step 1: Check Xcode Console Output
When the app hangs, look at Xcode console for:
```
🌐 [PasskeyService] POST https://idp.dev.internal/...
❌ Network error: ...
```

### Step 2: Test Network Connectivity
Add this temporary code to `DemoApp.swift` to test network on startup:

```swift
@main
struct IdpMobileDemoApp: App {
    init() {
        // Test DNS resolution
        Task {
            do {
                let url = URL(string: "https://api.dev.internal:5002")!
                let (_, response) = try await URLSession.shared.data(from: url)
                print("✅ API reachable: \(response)")
            } catch {
                print("❌ API unreachable: \(error)")
            }
        }
    }
    
    var body: some Scene {
        WindowGroup {
            ContentView()
        }
    }
}
```

### Step 3: Disable Network Checks Temporarily
Comment out the API client initialization to see if that's the issue:

In `ContentView.swift:236-239`:
```swift
override init() {
    super.init()
    // self.apiClient = ApiClient(oauthClient: oauthClient)
    // checkAuthenticationStatus()
}
```

Then manually initialize later when needed.

## Expected Behavior

### Normal Startup (< 2 seconds)
1. App launches
2. Shows "IdP Mobile Client" screen
3. "Sign in with Passkey" button visible
4. No errors

### Slow Startup (DNS Issue)
1. App shows blank screen
2. Waits 30-60 seconds (DNS timeout)
3. Eventually shows UI or crashes
4. Console shows DNS/network errors

## Resolution Checklist

- [ ] iPhone DNS configured to `192.168.178.153`
- [ ] Airplane mode toggled to clear DNS cache
- [ ] Safari can access `http://api.dev.internal:5002` (shows connection error, not "Cannot Find Server")
- [ ] Safari can access `http://idp.dev.internal:5001` (shows IdentityServer page)
- [ ] Certificate installed on iPhone
- [ ] Certificate trusted in Certificate Trust Settings
- [ ] Clean build in Xcode
- [ ] App deleted from iPhone before reinstalling

## If Still Hanging

### Option 1: Use IP Address Temporarily
Edit `ApiClient.swift:9` and `OAuthClient.swift:12`:

```swift
// ApiClient.swift
apiBaseURL: String = "https://192.168.178.153:5002"

// OAuthClient.swift
idpBaseURL: String = "https://192.168.178.153:5001"
```

This bypasses DNS entirely. If the app starts quickly with IPs, it confirms DNS is the issue.

### Option 2: Increase Network Timeout
Add to `Info.plist`:
```xml
<key>NSURLSessionConfiguration</key>
<dict>
    <key>timeoutIntervalForRequest</key>
    <integer>10</integer>
</dict>
```

This reduces timeout from 60s to 10s (app will fail faster if DNS is broken).

### Option 3: Use DNS Override App
Install "DNS Override" from App Store:
1. Add rule: `api.dev.internal` → `192.168.178.153`
2. Add rule: `idp.dev.internal` → `192.168.178.153`
3. Enable profile
4. Try app again

## Common Error Messages

### "Cannot Find Server"
- **Cause:** DNS not resolving
- **Fix:** Configure iPhone DNS to `192.168.178.153`

### "The certificate for this server is invalid"
- **Cause:** Certificate not installed or not trusted
- **Fix:** Install `api-dev-cert.cer` and trust it

### "The request timed out"
- **Cause:** DNS timeout or server not running
- **Fix:** Clear DNS cache, verify server is running

### "The network connection was lost"
- **Cause:** SSL handshake failed
- **Fix:** Trust certificate in Certificate Trust Settings

## Next Steps After DNS Fix

1. **Clear DNS cache** (airplane mode toggle)
2. **Test in Safari** (`http://api.dev.internal:5002`)
3. **Clean build** in Xcode
4. **Run app** - should start in < 2 seconds
5. **Sign in** with passkey
6. **Call API** - should work without errors

## Summary

**Most likely issue:** DNS cache on iPhone doesn't have `api.dev.internal` entry yet.

**Quick fix:** Toggle airplane mode on/off to clear DNS cache.

**Test:** Safari should be able to access `http://api.dev.internal:5002` (shows connection error, not "Cannot Find Server").

**Then:** App should start quickly without hanging.

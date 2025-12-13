# Getting Started with IdpMobileClient

## 🚀 Quick Start (5 minutes)

### Step 1: Generate Xcode Project
```bash
cd /Users/chris.keogh/dev/Samples/IdentityServer/v7/AspNetIdentityPasskeys/IdpMobileClient
./setup-xcode.sh
./install-cert-to-simulator.sh
```

This will generate the Xcode project and install the SSL certificate to the simulator.

### Step 2: Configure Development Team
1. Xcode will open automatically
2. Select the project in the navigator
3. Select the `IdpMobileDemoApp` target
4. Go to "Signing & Capabilities"
5. Select your Development Team

### Step 3: Build and Run
1. Select an iOS 18 simulator or device
2. Press ⌘R to build and run
3. The app will launch

### Step 4: Test Authentication
1. Tap "Sign in with Passkey"
2. Complete the biometric authentication
3. You should see "✓ Signed In"

### Step 5: Test API Call
1. Tap "Call API"
2. The API response should display

## 📋 Prerequisites Checklist

Before you start, ensure you have:

- [ ] macOS with Xcode 15.0 or later
- [ ] iOS 18 simulator or device
- [ ] IdentityServer running at `https://idp.dev.internal:5000`
- [ ] API running at `https://api.dev.internal:5002`
- [ ] OAuth client configured on IdP (see below)

## 🔧 IdP Configuration

Your IdentityServer needs this client configuration:

```csharp
new Client
{
    ClientId = "mobile-client",
    ClientName = "iOS Mobile Client",
    
    AllowedGrantTypes = GrantTypes.Code,
    RequirePkce = true,
    RequireClientSecret = false,
    
    RedirectUris = { "com.idp.mobile://callback" },
    PostLogoutRedirectUris = { "com.idp.mobile://callback" },
    
    AllowedScopes = {
        IdentityServerConstants.StandardScopes.OpenId,
        IdentityServerConstants.StandardScopes.Profile,
        IdentityServerConstants.StandardScopes.Email,
        "api",
        IdentityServerConstants.StandardScopes.OfflineAccess
    },
    
    AllowOfflineAccess = true,
    AccessTokenLifetime = 3600
}
```

And these WebAuthn endpoints:
- `POST /api/passkey/authenticate/begin`
- `POST /api/passkey/authenticate/complete`

See `IDP_CONFIGURATION.md` for complete details.

## 📱 What You'll See

### Initial Screen
```
┌─────────────────────────────┐
│      IdP Mobile Client      │
│                             │
│    🔑 (Passkey Icon)        │
│                             │
│   IdP Mobile Client         │
│                             │
│ Sign in with your passkey   │
│      to continue            │
│                             │
│  ┌───────────────────────┐  │
│  │ 🔑 Sign in with       │  │
│  │    Passkey            │  │
│  └───────────────────────┘  │
│                             │
└─────────────────────────────┘
```

### After Sign-In
```
┌─────────────────────────────┐
│      IdP Mobile Client      │
│                             │
│    ✓ Signed In              │
│    Welcome, user@email.com  │
│                             │
│  ┌───────────────────────┐  │
│  │ 🌐 Call API           │  │
│  └───────────────────────┘  │
│                             │
│  API Response:              │
│  ┌───────────────────────┐  │
│  │ { "data": "..." }     │  │
│  └───────────────────────┘  │
│                             │
│         Sign Out            │
└─────────────────────────────┘
```

## 🎯 What Happens Behind the Scenes

### When You Tap "Sign in with Passkey":

1. **App → IdP**: Request authentication challenge
2. **iOS**: Show passkey prompt with Face ID/Touch ID
3. **User**: Authenticate with biometrics
4. **App → IdP**: Send signed assertion
5. **IdP → App**: Return authorization code
6. **App → IdP**: Exchange code for tokens (PKCE)
7. **App**: Store tokens in Keychain
8. **App**: Show "Signed In" state

### When You Tap "Call API":

1. **App**: Check if access token is valid
2. **App**: If expired, refresh using refresh token
3. **App → API**: Make request with Bearer token
4. **API**: Validate token with IdP
5. **API → App**: Return protected resource
6. **App**: Display response

## 🔍 Troubleshooting

### "xcodegen: command not found"
```bash
brew install xcodegen
```

### "No Development Team"
1. Open Xcode preferences
2. Go to Accounts
3. Add your Apple ID
4. Select your team in project settings

### "Cannot connect to IdP" or SSL Certificate Errors
- Verify IdP is running: `curl https://idp.dev.internal:5001/.well-known/openid-configuration`
- Check DNS: Add to `/etc/hosts` if needed
- **Install SSL certificate**: Run `./install-cert-to-simulator.sh`
- If using a new simulator, reinstall the certificate

### "Passkey prompt doesn't appear"
- Ensure iOS 15+ device/simulator
- Check relying party identifier matches IdP domain
- Verify associated domains configured

### "API call fails"
- Check API is running
- Verify Bearer token is being sent
- Check API validates tokens correctly

## 📚 Next Steps

### Customize the App

1. **Change URLs**: Edit `OAuthClient.swift`, `PasskeyAuthService.swift`, `ApiClient.swift`
2. **Modify UI**: Edit `ContentView.swift`
3. **Add Features**: Extend `ApiClient.swift` with new endpoints
4. **Customize Branding**: Update colors, icons, text

### Learn More

- **Architecture**: Read `OVERVIEW.md`
- **Configuration**: Read `IDP_CONFIGURATION.md`
- **Quick Reference**: Read `QUICK_REFERENCE.md`
- **Detailed Setup**: Read `SETUP.md`

### Deploy to Production

1. Update URLs to production domains
2. Use valid SSL certificates
3. Configure associated domains
4. Set appropriate token lifetimes
5. Add error handling and logging
6. Test on physical devices
7. Submit to App Store

## 🎓 Understanding the Code

### Key Files to Explore

1. **`OAuthClient.swift`** (150 lines)
   - PKCE implementation
   - Token exchange
   - Token refresh

2. **`PasskeyAuthService.swift`** (200 lines)
   - WebAuthn integration
   - Passkey authentication
   - iOS AuthenticationServices

3. **`TokenStorage.swift`** (100 lines)
   - Keychain operations
   - Token management
   - Secure storage

4. **`ApiClient.swift`** (80 lines)
   - Authenticated requests
   - Bearer token handling
   - Error handling

5. **`ContentView.swift`** (200 lines)
   - SwiftUI interface
   - View model
   - User interactions

## ✅ Success Checklist

You're ready when:

- [x] Project created in `IdpMobileClient/` folder
- [ ] Xcode project generated successfully
- [ ] Project opens in Xcode
- [ ] Development Team configured
- [ ] App builds without errors
- [ ] App runs on iOS 18 simulator
- [ ] Passkey prompt appears on sign-in
- [ ] Authentication completes successfully
- [ ] "Signed In" state displays
- [ ] API call succeeds
- [ ] API response displays
- [ ] Sign-out works correctly

## 🆘 Need Help?

1. Check the troubleshooting section above
2. Review `QUICK_REFERENCE.md` for common tasks
3. Read `OVERVIEW.md` for architecture details
4. Check IdentityServer logs for auth issues
5. Use Xcode debugger for app issues

## 🎉 You're Ready!

The iOS mobile client is now set up and ready to use. You have:

✅ Native iOS 18 Swift application
✅ Passkey authentication
✅ PKCE OAuth2 flow
✅ Refresh token support
✅ Authenticated API calls
✅ Secure token storage
✅ Modern SwiftUI interface

**Happy coding!** 🚀

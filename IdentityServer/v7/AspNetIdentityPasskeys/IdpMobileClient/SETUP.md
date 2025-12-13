# IdpMobileClient Setup Guide

## Overview

This Swift package provides iOS 18 native passkey authentication with IdentityServer using PKCE OAuth2 flow.

## Creating the Xcode Project

Since Swift Package Manager doesn't fully support iOS app targets, you'll need to create an Xcode project manually:

### Option 1: Quick Setup (Recommended)

Run this command in the `IdpMobileClient` directory:

```bash
# Create Xcode project
xcodegen generate
```

Then open `IdpMobileClient.xcodeproj` in Xcode.

### Option 2: Manual Setup

1. Open Xcode
2. Create a new iOS App project:
   - File → New → Project
   - Choose "iOS" → "App"
   - Product Name: `IdpMobileDemoApp`
   - Organization Identifier: `com.idp.mobile`
   - Interface: SwiftUI
   - Language: Swift
   - Save in: `IdpMobileClient/` directory

3. Add the Swift Package:
   - File → Add Package Dependencies
   - Click "Add Local..."
   - Select the `IdpMobileClient` folder
   - Add the `IdpMobileClient` library to your target

4. Replace the generated files:
   - Delete the default `ContentView.swift` and `App.swift`
   - Add `Sources/IdpMobileDemoApp/DemoApp.swift` to your target
   - Add `Sources/IdpMobileDemoApp/ContentView.swift` to your target

5. Configure capabilities:
   - Select your target → Signing & Capabilities
   - Add "Associated Domains" capability
   - Add domain: `webcredentials:idp.dev.internal`

6. Update Info.plist:
   - Copy settings from `Sources/IdpMobileDemoApp/Info.plist`
   - Ensure URL scheme `com.idp.mobile` is configured
   - Ensure ATS exceptions for `idp.dev.internal` and `api.dev.internal`

### Option 3: Use XcodeGen (Automated)

Create a `project.yml` file (see below) and run `xcodegen generate`.

## XcodeGen Configuration

Create `project.yml` in the `IdpMobileClient` directory:

```yaml
name: IdpMobileClient
options:
  bundleIdPrefix: com.idp.mobile
  deploymentTarget:
    iOS: "18.0"

packages:
  IdpMobileClient:
    path: .

targets:
  IdpMobileDemoApp:
    type: application
    platform: iOS
    deploymentTarget: "18.0"
    sources:
      - Sources/IdpMobileDemoApp
    dependencies:
      - package: IdpMobileClient
    info:
      path: Sources/IdpMobileDemoApp/Info.plist
      properties:
        CFBundleIdentifier: com.idp.mobile.demo
        CFBundleShortVersionString: "1.0"
        CFBundleVersion: "1"
        UILaunchScreen: {}
        UIApplicationSceneManifest:
          UIApplicationSupportsMultipleScenes: true
        CFBundleURLTypes:
          - CFBundleTypeRole: Editor
            CFBundleURLName: com.idp.mobile
            CFBundleURLSchemes:
              - com.idp.mobile
    settings:
      base:
        PRODUCT_BUNDLE_IDENTIFIER: com.idp.mobile.demo
        DEVELOPMENT_TEAM: YOUR_TEAM_ID
```

Then run:
```bash
brew install xcodegen  # If not installed
xcodegen generate
open IdpMobileClient.xcodeproj
```

## Configuration

### IdP Endpoints

The app is configured to use:
- **IdP**: `https://idp.dev.internal:5000`
- **API**: `https://api.dev.internal:5002`

To change these, edit:
- `Sources/IdpMobileClient/OAuthClient.swift` - `idpBaseURL` default
- `Sources/IdpMobileClient/PasskeyAuthService.swift` - `idpBaseURL` and `relyingPartyIdentifier` defaults
- `Sources/IdpMobileClient/ApiClient.swift` - `apiBaseURL` default

### OAuth Client Configuration

Default OAuth settings in `OAuthClient.swift`:
- **Client ID**: `mobile-client`
- **Redirect URI**: `com.idp.mobile://callback`
- **Scopes**: `openid profile email api offline_access`

Update these in your IdP's client configuration to match.

## Running the App

1. Open the project in Xcode
2. Select an iOS 18 simulator or device
3. Build and run (⌘R)

### Testing Passkey Flow

1. Tap "Sign in with Passkey"
2. The app will:
   - Request authentication options from IdP
   - Present native iOS passkey prompt
   - Complete authentication with IdP
   - Exchange authorization code for tokens (with PKCE)
   - Store tokens securely in Keychain

3. Once authenticated, tap "Call API"
4. The app will:
   - Retrieve access token from Keychain
   - Make authenticated request to API
   - Display response

### Debugging

Enable network logging in Xcode:
- Product → Scheme → Edit Scheme
- Run → Arguments
- Add environment variable: `CFNETWORK_DIAGNOSTICS = 3`

## Requirements

- Xcode 15.0+
- iOS 18.0+
- Swift 5.9+
- IdP must support:
  - WebAuthn/Passkey endpoints
  - PKCE OAuth2 flow
  - Refresh tokens

## Architecture

```
IdpMobileClient/
├── Package.swift                 # Swift package definition
├── Sources/
│   ├── IdpMobileClient/         # Core library
│   │   ├── OAuthClient.swift    # PKCE OAuth2 client
│   │   ├── TokenStorage.swift   # Keychain token storage
│   │   ├── PasskeyAuthService.swift  # Passkey authentication
│   │   └── ApiClient.swift      # Authenticated API calls
│   └── IdpMobileDemoApp/        # Demo iOS app
│       ├── DemoApp.swift        # App entry point
│       ├── ContentView.swift    # Main UI
│       └── Info.plist           # App configuration
└── README.md
```

## Troubleshooting

### "Cannot find 'IdpMobileClient' in scope"

Ensure the package is added to your target's dependencies in Xcode.

### Passkey prompt doesn't appear

1. Check that your device/simulator supports passkeys (iOS 15+)
2. Verify the relying party identifier matches your IdP domain
3. Check that associated domains are configured correctly

### Network errors

1. Verify the IdP and API URLs are correct
2. Check that ATS exceptions are configured in Info.plist
3. Ensure your device can reach the internal domains (may need VPN or hosts file)

### Token refresh fails

1. Check that `offline_access` scope is requested
2. Verify IdP returns refresh tokens
3. Check token storage in Keychain (use Keychain Access app)

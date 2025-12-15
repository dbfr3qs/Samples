# iOS Mobile Client - Build Notes

## Important: iOS-Only Library

This Swift package is designed for **iOS 17+** and contains iOS-specific APIs (AuthenticationServices, CryptoKit with iOS features). It cannot be built directly using `swift build` on macOS as that command defaults to building for macOS.

## Building the iOS Client

### Option 1: Build via Xcode (Recommended)

1. Open the Xcode project:
   ```bash
   open IdpMobileClient.xcodeproj
   ```

2. Select an iOS simulator or device as the build destination

3. Build the project (⌘B)

### Option 2: Build for iOS Simulator via Command Line

```bash
xcodebuild -scheme IdpMobileClient -destination 'platform=iOS Simulator,name=iPhone 15 Pro' build
```

### Option 3: Generate Xcode Project from Package

If you don't have the Xcode project, generate it:

```bash
swift package generate-xcodeproj
open IdpMobileClient.xcodeproj
```

## Platform Requirements

- **iOS**: 17.0+ (for PRF extension support)
- **iOS**: 16.0+ (for basic passkey authentication)
- **iOS**: 15.0+ (for DPoP and CryptoKit features)

## Why `swift build` Fails

The `swift build` command on macOS defaults to building for macOS, but this package uses iOS-specific APIs:

- `ASAuthorizationPlatformPublicKeyCredentialProvider` (iOS 16+)
- `ASAuthorizationController` (iOS 13+, but with iOS-specific usage)
- PRF extension APIs (iOS 17+)
- CryptoKit features with iOS-specific availability

These APIs are not available on macOS or have different availability requirements.

## Verification

To verify the implementation compiles correctly:

1. Open in Xcode
2. Select iOS Simulator as destination
3. Build (⌘B)
4. Run tests if available

## Integration

To integrate this library into your iOS app:

1. Add as a local Swift package dependency in Xcode
2. Or copy the source files directly into your iOS project
3. Ensure your app targets iOS 15.0+ minimum

## Components Implemented

✅ **SecureStorage.swift** - Keychain storage for PRF output and DPoP keys  
✅ **DPoPKeyManager.swift** - HKDF key derivation and JWK generation  
✅ **DPoPProofGenerator.swift** - RFC 9449 compliant proof generation  
✅ **OAuthClient.swift** - Enhanced with DPoP support  
✅ **PasskeyAuthService.swift** - Enhanced with PRF extension support  
✅ **AuthenticatedWebView.swift** - WebView SSO with id_token_hint  
✅ **ApiClient.swift** - Authenticated API requests  

All components have proper `@available` attributes for iOS 15.0+/macOS 12.0+.

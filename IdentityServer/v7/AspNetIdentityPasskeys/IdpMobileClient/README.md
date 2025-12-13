# IdpMobileClient

iOS 18 Swift client for authenticating with IdentityServer using passkeys and PKCE OAuth2 flow.

## Features

- **Passkey Authentication**: Native iOS passkey support for WebAuthn-based sign-in
- **PKCE OAuth2 Flow**: Authorization Code flow with PKCE and refresh tokens
- **Secure Token Storage**: Access and refresh tokens stored in iOS Keychain
- **API Integration**: Authenticated API calls using Bearer tokens

## Quick Start

```bash
cd IdpMobileClient
./setup-xcode.sh
./install-cert-to-simulator.sh  # Install SSL certificate to simulator
```

This will:
1. Install XcodeGen (if needed)
2. Generate the Xcode project
3. Open it in Xcode
4. Install the ASP.NET Core dev certificate to the simulator

Then:
1. Select the `IdpMobileDemoApp` target
2. Update your Development Team in Signing & Capabilities
3. Build and run on iOS 17+ simulator or device

## Configuration

The client is configured to work with:
- **IdP**: `https://idp.dev.internal:5000`
- **API**: `https://api.dev.internal:5002`
- **Client ID**: `mobile-client`
- **Redirect URI**: `com.idp.mobile://callback`

## Package Structure

- **IdpMobileClient**: Core library with OAuth and passkey logic
- **IdpMobileDemoApp**: Demo iOS app showing sign-in and API calls

### Running the Demo App

1. Tap "Sign in with Passkey" to authenticate
2. Tap "Call API" to make an authenticated request to the API

## Architecture

- **OAuthClient**: Handles PKCE flow, token exchange, and refresh
- **PasskeyAuthService**: Manages passkey registration and authentication
- **TokenStorage**: Secure Keychain-based token persistence
- **ApiClient**: Makes authenticated API requests

# AspNetIdentityPasskeys Setup Guide

This guide explains how to set up and run the AspNetIdentityPasskeys example, which demonstrates a theoretical way to authenticate mobile webviews authentication using WebAuthn/Passkeys with DPoP token binding, server side sessions and PRF-based device binding.

## Overview

This example includes:
- **IdentityServer** with passkey authentication, DPoP validation, and mobile session management
- **API** protected by DPoP-bound tokens
- **WebView App** demonstrating SSO via session exchange
- **iOS Mobile Client** with native passkey authentication, DPoP proofs, and WebView integration

## Prerequisites

- .NET 10.0 SDK or later
- macOS (for iOS development)
- Xcode 15.0+ (for mobile client)
- iOS 18+ device or simulator (for PRF extension support)
- Make sure all apps are running on the same network

## Quick Start

### 1. Start IdentityServer

```bash
cd IdentityServerAspNetIdentityPasskeys
dotnet run
```

The server will run on:
- `https://localhost:5001`
- `https://idp.dev.internal:5001` (for mobile testing)

### 2. Access the Application

Open your browser and navigate to:
```
https://localhost:5001
```

### 3. Test Passkey Authentication

1. Register a new user or log in with existing credentials
2. Navigate to `/Account/Passkeys` to manage passkeys
3. Click "Add a new passkey" to register a passkey
4. Use Face ID, Touch ID, or Windows Hello to create the passkey
5. Navigate to `/Account/PrfDemo` to test the PRF (Pseudo-Random Function) extension

## Running the API

The API is a protected resource that requires DPoP-bound tokens from IdentityServer.

```bash
cd Api
dotnet run
```

The API will run on:
- `https://localhost:5002`
- `https://api.dev.internal:5002` (for mobile testing)

### Test the API

```bash
# Without authentication (should return 401)
curl -k https://localhost:5002/claims

# With DPoP-bound token (requires DPoP proof header)
curl -k -H "Authorization: DPoP YOUR_ACCESS_TOKEN" \
     -H "DPoP: YOUR_DPOP_PROOF" \
     https://localhost:5002/claims
```

## Running the WebView App

The WebView app demonstrates SSO via session exchange from the mobile app.

```bash
cd WebViewApp
dotnet run
```

The app will run on:
- `https://localhost:5003`
- `https://web.dev.internal:5003` (for mobile testing)

The mobile app injects authentication cookies into a WebView that loads this app, demonstrating seamless SSO without requiring the user to log in again.

## iOS Mobile Client Setup

The iOS mobile client demonstrates native passkey authentication on iOS 18+.

### Prerequisites

- macOS with Xcode 15.0+
- iOS 18+ simulator or device
- XcodeGen (install with `brew install xcodegen`)

### Quick Setup

```bash
cd IdpMobileClient
./setup-xcode.sh
```

This will:
1. Generate the Xcode project
2. Install SSL certificates to the simulator

### Configure Development Team

1. Xcode will open automatically
2. Select the project in the navigator
3. Select the `IdpMobileDemoApp` target
4. Go to "Signing & Capabilities"
5. Select your Development Team

### Build and Run

1. Select an iOS 18 simulator or device
2. Press ⌘R to build and run
3. Tap "Sign in with Passkey" to authenticate
4. Tap "Call API" to test DPoP-protected API calls
5. Tap "Open WebView" to test SSO via session exchange

## iPhone Testing (Local Network)

To test on a physical iPhone, you need to configure local network access.

### Step 1: Configure DNS

Set up dnsmasq to resolve custom domains:

```bash
# Install dnsmasq
brew install dnsmasq

# Get your Mac's local IP
ipconfig getifaddr en0
# Example: 192.168.1.100

# Configure dnsmasq
echo "address=/idp.dev.internal/YOUR_MAC_IP" | sudo tee -a /opt/homebrew/etc/dnsmasq.conf
echo "address=/api.dev.internal/YOUR_MAC_IP" | sudo tee -a /opt/homebrew/etc/dnsmasq.conf
echo "listen-address=127.0.0.1,YOUR_MAC_IP" | sudo tee -a /opt/homebrew/etc/dnsmasq.conf
echo "bind-interfaces" | sudo tee -a /opt/homebrew/etc/dnsmasq.conf

# Start dnsmasq
sudo brew services start dnsmasq
```

### Step 2: Generate SSL Certificates

Generate certificates for the custom domains:

```bash
# For IdentityServer
./export-cert-for-iphone.sh

# For API
./generate-api-cert.sh
./export-api-cert-for-iphone.sh

# For WebView
./generate-web-cert.sh
./export-web-cert-for-iphone.sh

```

Certificates will be exported to your Desktop.

### Step 3: Install Certificates on iPhone

1. **Transfer certificates to iPhone** (via AirDrop, email, or iCloud)
   - `aspnetcore-dev-cert.cer` (for IdentityServer)
   - `api-dev-cert.cer` (for API)
   - `web-dev-cert.cer` (for WebView)

2. **Install each certificate:**
   - Tap the certificate file
   - Settings → General → VPN & Device Management
   - Tap the certificate → Install (3 times) → Done

3. **Trust the certificates:**
   - Settings → General → About → Certificate Trust Settings
   - Toggle ON for each certificate
   - Tap Continue

### Step 4: Configure iPhone DNS

1. Settings → Wi-Fi → (i) next to your network
2. Configure DNS → Manual
3. Remove existing DNS servers
4. Add your Mac's IP address (e.g., 192.168.1.100)
5. Save

### Step 5: Update Configuration

Update the IdentityServer and API to listen on custom domains:

**IdentityServer launchSettings.json:**
```json
{
  "applicationUrl": "https://idp.dev.internal:5001;https://localhost:5001"
}
```

**API launchSettings.json:**
```json
{
  "applicationUrl": "https://api.dev.internal:5002;https://localhost:5002",
  "environmentVariables": {
    "ASPNETCORE_Kestrel__Certificates__Default__Path": "certs/api-dev-cert.pfx",
    "ASPNETCORE_Kestrel__Certificates__Default__Password": "dev-password"
  }
}
```

### Step 6: Test from iPhone

1. Start IdentityServer and API on your Mac
2. Open Safari on iPhone
3. Navigate to `https://idp.dev.internal:5001`
4. Test passkey authentication
5. Open the mobile app and test API calls

## Configuration

### IdentityServer Client Configuration

The mobile client requires this OAuth client configuration with DPoP support:

```csharp
new Client
{
    ClientId = "mobile-client",
    ClientName = "iOS Mobile Client",
    
    AllowedGrantTypes = GrantTypes.Code,
    RequirePkce = true,
    RequireClientSecret = false,
    RequireDPoP = true,  // Require DPoP for token binding
    
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

The WebView app requires this client configuration:

new Client
{
    ClientId = "webview-client",
    ClientName = "WebView Client",
    
    AllowedGrantTypes = GrantTypes.Code,
    RequirePkce = true,
    RequireClientSecret = false,
    
    RedirectUris = { "https://web.dev.internal:5003/signin-oidc" },
    PostLogoutRedirectUris = { "https://web.dev.internal:5003/signout-callback-oidc" },
    
    AllowedScopes = {
        IdentityServerConstants.StandardScopes.OpenId,
        IdentityServerConstants.StandardScopes.Profile
    }
}
```

### Mobile Client Configuration

Default configuration in the mobile client:
- **IdP URL**: `https://idp.dev.internal:5001`
- **API URL**: `https://api.dev.internal:5002`
- **WebView URL**: `https://web.dev.internal:5003`
- **Client ID**: `mobile-client`
- **Redirect URI**: `com.idp.mobile://callback`
- **Scopes**: `openid profile email api offline_access`
- **DPoP**: Enabled (tokens bound to device-specific keys)
- **PRF Extension**: Enabled (for device binding)

To change these, edit:
- `Sources/IdpMobileClient/OAuthClient.swift`
- `Sources/IdpMobileClient/PasskeyAuthService.swift`
- `Sources/IdpMobileClient/ApiClient.swift`
- `Sources/IdpMobileDemoApp/ContentView.swift`

## Troubleshooting

### IdentityServer Issues

**Certificate warnings in browser:**
- Trust the ASP.NET Core development certificate
- Run: `dotnet dev-certs https --trust`

**Passkey registration fails:**
- Ensure you're using HTTPS
- Check browser console for errors
- Verify the origin validation in `HostingExtensions.cs`

### API Issues

**401 Unauthorized:**
- This is expected without a valid access token
- Obtain a token from IdentityServer first

**Cannot connect to API:**
- Verify the API is running
- Check firewall settings
- Ensure the authority URL in `Program.cs` is correct

### Mobile Client Issues

**Cannot connect to IdP:**
- Verify IdentityServer is running
- Check DNS resolution (for custom domains)
- Install SSL certificates on simulator/device
- Run `./install-cert-to-simulator.sh` for simulators

**Passkey prompt doesn't appear:**
- Ensure iOS 15+ device/simulator
- Check relying party identifier matches IdP domain
- Verify associated domains are configured

**API call fails:**
- Check API is running
- Verify access token is valid
- Check network connectivity

### iPhone Testing Issues

**DNS not resolving:**
```bash
# Test DNS from Mac
nslookup idp.dev.internal YOUR_MAC_IP

# Restart dnsmasq
sudo brew services restart dnsmasq
```

**Certificate not trusted:**
- Verify certificate is installed in Settings → General → VPN & Device Management
- Verify certificate is trusted in Settings → General → About → Certificate Trust Settings
- Restart Safari after trusting

**Cannot connect from iPhone:**
- Verify both devices are on the same WiFi network
- Check Mac firewall settings:
  ```bash
  sudo /usr/libexec/ApplicationFirewall/socketfilterfw --listapps | grep dotnet
  sudo /usr/libexec/ApplicationFirewall/socketfilterfw --unblockapp /usr/local/share/dotnet/dotnet
  ```

**IP address changed:**
1. Update dnsmasq configuration with new IP
2. Regenerate SSL certificates
3. Reinstall certificates on iPhone
4. Update iPhone DNS settings
5. Restart dnsmasq

## Security Notes

⚠️ **Development Only**: This setup uses self-signed certificates and is intended for development/testing only.

⚠️ **Certificate Expiry**: Development certificates expire after 1 year and will need to be regenerated.

⚠️ **Network Access**: When running on your local network, anyone on the network can access the services.

⚠️ **Production Deployment**: For production, use valid SSL certificates from a trusted CA and secure your endpoints appropriately.

## Architecture

### Components

- **IdentityServerAspNetIdentityPasskeys**: ASP.NET Core application with IdentityServer and passkey support
- **Api**: Protected API resource that validates tokens from IdentityServer
- **IdpMobileClient**: iOS Swift package with native passkey authentication

### Authentication Flow

1. User initiates sign-in on mobile client
2. Client requests authentication challenge from IdentityServer
3. iOS presents native passkey prompt (Face ID/Touch ID)
4. User authenticates with biometrics
5. Client sends signed assertion to IdentityServer
6. IdentityServer validates assertion and returns authorization code
7. Client exchanges code for tokens using PKCE
8. Client stores tokens securely in Keychain
9. Client uses access token to call protected API

### Key Features

- **WebAuthn/Passkeys**: Passwordless authentication using FIDO2 standards
- **PRF Extension**: Derives device-specific keys for DPoP binding
- **DPoP Token Binding**: Tokens cryptographically bound to device keys
- **Device Binding**: Server verifies DPoP key matches the device that authenticated
- **Session Exchange**: Secure cookie exchange for WebView SSO
- **PKCE**: Proof Key for Code Exchange for secure OAuth flows
- **Token Management**: Automatic token refresh with DPoP proofs
- **Native iOS Integration**: Uses iOS AuthenticationServices framework

## Additional Resources

For more detailed information about the implementation, see:
- **Main README**: `README.md` - Comprehensive overview of features and architecture
- **Source Code**: All components include inline documentation

## Support

If you encounter issues:
1. Check the troubleshooting section above
2. Verify all prerequisites are installed
3. Check IdentityServer logs for authentication errors
4. Use browser developer tools to inspect network requests
5. Use Xcode debugger for mobile client issues

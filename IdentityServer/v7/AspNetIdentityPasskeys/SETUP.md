# AspNetIdentityPasskeys Setup Guide

This guide explains how to set up and run the AspNetIdentityPasskeys ios native passkeys example, which demonstrates passkey authentication using a mobile app natively, rather than a browser-based flow. This implementation uses the netfido2 library for server-side passkey handling.

## Overview

This example includes:
- **IdentityServer** with passkey authentication support
- **API** protected by IdentityServer
- **iOS Mobile Client** with native passkey authentication

## Prerequisites

- .NET 8.0 SDK or later
- macOS (for iOS development)
- Xcode 15.0+ (for mobile client)
- iOS 18+ device or simulator (for mobile client)

## Quick Start

### 1. Start IdentityServer

```bash
cd IdentityServerAspNetIdentityPasskeys
dotnet run
```

The server will run on `https://localhost:5001`

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

The API is a protected resource that requires authentication from IdentityServer.

```bash
cd Api
dotnet run
```

The API will run on `https://localhost:5002`

### Test the API

```bash
# Without authentication (should return 401)
curl -k https://localhost:5002/claims

# With authentication (requires access token from IdentityServer)
curl -k -H "Authorization: Bearer YOUR_ACCESS_TOKEN" https://localhost:5002/claims
```

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
4. Tap "Call API" to test authenticated API calls

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
```

Certificates will be exported to your Desktop.

### Step 3: Install Certificates on iPhone

1. **Transfer certificates to iPhone** (via AirDrop, email, or iCloud)
   - `aspnetcore-dev-cert.cer` (for IdentityServer)
   - `api-dev-cert.cer` (for API)

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

The mobile client requires this OAuth client configuration:

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

### Mobile Client Configuration

Default configuration in the mobile client:
- **IdP URL**: `https://idp.dev.internal:5001`
- **API URL**: `https://api.dev.internal:5002`
- **Client ID**: `mobile-client`
- **Redirect URI**: `com.idp.mobile://callback`
- **Scopes**: `openid profile email api offline_access`

To change these, edit:
- `Sources/IdpMobileClient/OAuthClient.swift`
- `Sources/IdpMobileClient/PasskeyAuthService.swift`
- `Sources/IdpMobileClient/ApiClient.swift`

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
- **PRF Extension**: Pseudo-Random Function extension for deriving encryption keys
- **PKCE**: Proof Key for Code Exchange for secure OAuth flows
- **Token Management**: Automatic token refresh and secure storage
- **Native iOS Integration**: Uses iOS AuthenticationServices framework

## Additional Resources

For more detailed information, see:
- **Mobile Client**: `IdpMobileClient/README.md`
- **API**: `Api/README.md`
- **PRF Extension**: `IdentityServerAspNetIdentityPasskeys/Passkeys/PRF_EXTENSION.md`

## Support

If you encounter issues:
1. Check the troubleshooting section above
2. Verify all prerequisites are installed
3. Check IdentityServer logs for authentication errors
4. Use browser developer tools to inspect network requests
5. Use Xcode debugger for mobile client issues

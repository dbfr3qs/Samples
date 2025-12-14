# API iPhone Setup Guide

## Overview
This guide explains how to set up the API to be accessible from your iPhone on the local network with proper DNS resolution and certificate trust.

## Prerequisites
- Mac and iPhone on the same WiFi network
- Mac IP: `192.168.178.153`
- dnsmasq running on Mac
- iPhone configured to use Mac as DNS server

## Architecture
- **IdentityServer**: `https://idp.dev.internal:5001`
- **API**: `https://api.dev.internal:5002`
- **Mobile App**: iOS app using passkey authentication

## Setup Steps

### 1. DNS Configuration (Already Done)
The following DNS entries are configured in dnsmasq:
```
address=/idp.dev.internal/192.168.178.153
address=/api.dev.internal/192.168.178.153
```

### 2. Generate API Certificate
Run the certificate generation script:
```bash
./generate-api-cert.sh
```

This creates:
- `Api/certs/api-dev-cert.pfx` - Certificate for .NET API
- `Api/certs/api-dev-cert.cer` - Certificate for iPhone installation
- Certificate includes SANs: `api.dev.internal`, `*.dev.internal`, `localhost`, IP addresses

### 3. Install Certificate on iPhone

#### Export Certificate to Desktop
```bash
./export-api-cert-for-iphone.sh
```

#### Transfer to iPhone
Choose one method:

**Option A: AirDrop (Easiest)**
1. Right-click `api-dev-cert.cer` on your Desktop
2. Select "Share" → "AirDrop"
3. Send to your iPhone

**Option B: Email**
1. Email the `api-dev-cert.cer` file to yourself
2. Open the email on your iPhone
3. Tap the attachment

**Option C: iCloud Drive**
1. Copy `api-dev-cert.cer` to iCloud Drive
2. Open Files app on iPhone
3. Navigate to the certificate and tap it

#### Install on iPhone
1. After receiving the certificate, tap it
2. You'll see "Profile Downloaded"
3. Go to **Settings** → **General** → **VPN & Device Management**
4. Under "Downloaded Profile", tap the certificate
5. Tap **Install** (top right)
6. Enter your passcode
7. Tap **Install** again (warning will appear)
8. Tap **Install** one more time
9. Tap **Done**

#### Trust the Certificate
1. Go to **Settings** → **General** → **About** → **Certificate Trust Settings**
2. Under "Enable Full Trust for Root Certificates"
3. Toggle ON the switch for "api.dev.internal"
4. Tap **Continue** on the warning

### 4. Verify iPhone DNS Configuration

On your iPhone:
1. Go to **Settings** → **Wi-Fi**
2. Tap the **(i)** icon next to your network
3. Scroll to **DNS**
4. Verify it shows: `192.168.178.153` (your Mac's IP)

If not configured:
1. Tap **Configure DNS** → **Manual**
2. Remove existing DNS servers
3. Add DNS Server: `192.168.178.153`
4. Tap **Save**

### 5. Start the API

```bash
cd Api
dotnet run
```

The API will listen on:
- `https://api.dev.internal:5002` (for iPhone)
- `https://localhost:5002` (for Mac)

### 6. Test from iPhone

Open Safari on your iPhone and navigate to:
```
https://api.dev.internal:5002/claims
```

You should see a 401 Unauthorized response (expected - endpoint requires authentication).
No certificate warnings should appear.

### 7. Test from Mobile App

The mobile app is already configured to use `https://api.dev.internal:5002`.

1. Open the mobile app on your iPhone
2. Sign in with passkey
3. Try calling the API endpoint
4. Should work without DNS or certificate errors

## API Endpoints

### GET /claims
Returns claims from the access token.

**Authentication**: Required (Bearer token)

**Response**:
```json
{
  "claims": [
    {"type": "sub", "value": "..."},
    {"type": "name", "value": "..."}
  ],
  "identity": "username",
  "isAuthenticated": true
}
```

## Troubleshooting

### DNS Not Resolving

**Test DNS resolution on Mac:**
```bash
dig @192.168.178.153 api.dev.internal
```

Should return `192.168.178.153`.

**Restart dnsmasq:**
```bash
sudo brew services restart dnsmasq
```

### Certificate Not Trusted

- Verify you completed the "Trust the Certificate" step
- Restart Safari after trusting the certificate
- Try clearing Safari cache: Settings → Safari → Clear History and Website Data

### API Not Accessible

**Check firewall on Mac:**
```bash
# Allow incoming connections on port 5002
sudo /usr/libexec/ApplicationFirewall/socketfilterfw --add /usr/local/share/dotnet/dotnet
sudo /usr/libexec/ApplicationFirewall/socketfilterfw --unblockapp /usr/local/share/dotnet/dotnet
```

**Verify API is running:**
```bash
curl -k https://localhost:5002/claims
```

Should return 401 Unauthorized.

### Mobile App Getting DNS Errors

1. Verify iPhone DNS is set to `192.168.178.153`
2. Test DNS resolution in Safari: `https://api.dev.internal:5002/claims`
3. Check dnsmasq is running: `sudo brew services list | grep dnsmasq`

### Certificate Warnings in Safari

- Verify certificate is installed: Settings → General → VPN & Device Management
- Verify certificate is trusted: Settings → General → About → Certificate Trust Settings
- Check certificate SAN includes `api.dev.internal`:
  ```bash
  openssl x509 -in Api/certs/api-dev-cert.crt -text -noout | grep DNS
  ```

### IP Address Changed

If your Mac's IP address changes:

1. Find new IP:
   ```bash
   ipconfig getifaddr en0
   ```

2. Update dnsmasq configuration:
   ```bash
   sudo nano /opt/homebrew/etc/dnsmasq.conf
   # Update the IP addresses
   sudo brew services restart dnsmasq
   ```

3. Regenerate certificate with new IP:
   ```bash
   ./generate-api-cert.sh
   ```

4. Reinstall certificate on iPhone

## Security Notes

⚠️ **Development Only**: This setup is for development/testing only. Never use self-signed certificates in production.

⚠️ **Certificate Expiry**: The certificate expires after 1 year. Regenerate when needed.

⚠️ **Network Security**: Anyone on your local network can access the API while it's running.

## Configuration Files

### API Launch Settings
`Api/Properties/launchSettings.json`:
- Listens on `https://api.dev.internal:5002` and `https://localhost:5002`
- Uses certificate from `certs/api-dev-cert.pfx`
- Authority set to `https://idp.dev.internal:5001`

### Mobile App Configuration
`IdpMobileClient/Sources/IdpMobileClient/ApiClient.swift`:
- API base URL: `https://api.dev.internal:5002`
- Uses OAuth client for token management

## Quick Reference

```bash
# Generate certificate
./generate-api-cert.sh

# Export certificate for iPhone
./export-api-cert-for-iphone.sh

# Start API
cd Api && dotnet run

# Test API from Mac
curl -k https://localhost:5002/claims

# Test DNS from Mac
dig @192.168.178.153 api.dev.internal

# Restart dnsmasq
sudo brew services restart dnsmasq
```

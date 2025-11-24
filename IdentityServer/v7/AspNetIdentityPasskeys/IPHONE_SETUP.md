# iPhone Local Network Testing Setup

## Overview
This guide explains how to access IdentityServer from your iPhone on the local network for testing passkeys.

## Prerequisites
- Mac and iPhone on the same WiFi network
- IdentityServer running on Mac
- Local IP: `192.168.178.149`

## Step 1: Export Development Certificate

On your Mac, export the ASP.NET Core development certificate:

```bash
# Export the certificate
dotnet dev-certs https -ep ~/Desktop/aspnetcore-dev-cert.pfx -p YourPassword

# Convert to .cer format for iOS
openssl pkcs12 -in ~/Desktop/aspnetcore-dev-cert.pfx -clcerts -nokeys -out ~/Desktop/aspnetcore-dev-cert.cer -passin pass:YourPassword
```

## Step 2: Transfer Certificate to iPhone

### Option A: AirDrop
1. Right-click `aspnetcore-dev-cert.cer` on your Desktop
2. Select "Share" → "AirDrop"
3. Send to your iPhone

### Option B: Email
1. Email the `aspnetcore-dev-cert.cer` file to yourself
2. Open the email on your iPhone
3. Tap the attachment

### Option C: iCloud Drive
1. Copy `aspnetcore-dev-cert.cer` to iCloud Drive
2. Open Files app on iPhone
3. Navigate to the certificate and tap it

## Step 3: Install Certificate on iPhone

1. After receiving the certificate, tap it
2. You'll see "Profile Downloaded"
3. Go to **Settings** → **General** → **VPN & Device Management**
4. Under "Downloaded Profile", tap the certificate
5. Tap **Install** (top right)
6. Enter your passcode
7. Tap **Install** again (warning will appear)
8. Tap **Install** one more time
9. Tap **Done**

## Step 4: Trust the Certificate

1. Go to **Settings** → **General** → **About** → **Certificate Trust Settings**
2. Under "Enable Full Trust for Root Certificates"
3. Toggle ON the switch for "localhost" or "ASP.NET Core HTTPS development certificate"
4. Tap **Continue** on the warning

### ⚠️ If Certificate Trust Settings is Empty

If you don't see "Enable Full Trust for Root Certificates" or the certificate doesn't appear:

**Option A: Re-export the certificate**
```bash
# Delete the old certificate from your Desktop
rm ~/Desktop/aspnetcore-dev-cert.cer

# Re-run the export script
./export-cert-for-iphone.sh

# Transfer and install again
```

**Option B: Accept the certificate warning in Safari**

Since the certificate might not appear in Trust Settings, you can:

1. Skip Step 4 for now
2. Continue to Step 5 and start the server
3. Open Safari on iPhone and go to `https://192.168.178.149:5001`
4. You'll see a certificate warning
5. Tap **Show Details** → **visit this website** → **Visit Website**
6. This will work for the current session

**Option C: Use the IP address workaround**

iOS may trust certificates for IP addresses differently. The warning acceptance in Safari (Option B) should work fine for development testing.

## Step 5: Start IdentityServer

On your Mac:

```bash
cd IdentityServerAspNetIdentityPasskeys
dotnet run
```

The server will now listen on:
- `https://localhost:5001` (for Mac)
- `https://192.168.178.149:5001` (for iPhone)

## Step 6: Access from iPhone

1. Open Safari on your iPhone
2. Navigate to: `https://192.168.178.149:5001`
3. You should see the IdentityServer home page
4. No certificate warnings should appear

## Testing Passkeys

### Register a Passkey
1. Navigate to: `https://192.168.178.149:5001/account/passkeys`
2. Log in if needed
3. Click "Add a new passkey"
4. Use Face ID or Touch ID to create the passkey

### Test PRF Extension
1. Navigate to: `https://192.168.178.149:5001/account/prfdemo`
2. Click "Authenticate with Passkey"
3. Use Face ID or Touch ID
4. View the PRF output

## Troubleshooting

### Certificate Not Trusted
- Make sure you completed Step 4 (Certificate Trust Settings)
- Restart Safari after trusting the certificate
- Try clearing Safari cache: Settings → Safari → Clear History and Website Data

### Cannot Connect
- Verify both devices are on the same WiFi network
- Check firewall settings on Mac:
  ```bash
  # Allow incoming connections on port 5001
  sudo /usr/libexec/ApplicationFirewall/socketfilterfw --add /usr/local/share/dotnet/dotnet
  sudo /usr/libexec/ApplicationFirewall/socketfilterfw --unblockapp /usr/local/share/dotnet/dotnet
  ```

### Passkey Not Working
- Ensure you're using Safari (not Chrome or other browsers)
- WebAuthn/Passkeys work best in Safari on iOS
- Make sure the origin validation is configured correctly in `HostingExtensions.cs`

### IP Address Changed
If your Mac's IP address changes:
1. Find new IP: `ipconfig getifaddr en0`
2. Update `launchSettings.json` with new IP
3. Update `HostingExtensions.cs` with new IP
4. Restart IdentityServer

## Security Notes

⚠️ **Development Only**: This setup is for development/testing only. Never use self-signed certificates in production.

⚠️ **Certificate Expiry**: The development certificate expires after 1 year. You'll need to regenerate and reinstall it.

⚠️ **Network Security**: Anyone on your local network can access the server while it's running.

## Alternative: Using ngrok

If you don't want to deal with certificates, you can use ngrok:

```bash
# Install ngrok
brew install ngrok

# Start IdentityServer on localhost
dotnet run

# In another terminal, expose it
ngrok http https://localhost:5001
```

Then use the ngrok HTTPS URL on your iPhone. Note: You'll need to update the origin validation for the ngrok URL.

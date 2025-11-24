# Using identity.web Domain for iPhone Testing

## Overview

The application is now configured to use `identity.web:5001` which your DNS provider automatically redirects to localhost. This solves the WebAuthn issue with IP addresses.

**Bonus:** The ASP.NET Core development certificate already includes `identity.web` in its Subject Alternative Name (SAN), so there will be **no certificate warnings**! 🎉

## Configuration Changes

✅ **launchSettings.json** - Server listens on `https://identity.web:5001`
✅ **HostingExtensions.cs** - Passkey origin validation allows `https://identity.web:5001`
✅ **Certificate** - `identity.web` already in SAN (no warnings!)

## Testing Steps

### 1. Start the Server

```bash
cd IdentityServerAspNetIdentityPasskeys
dotnet run
```

The server will now listen on:
- `https://localhost:5001` (for local Mac access)
- `https://identity.web:5001` (for iPhone access)

### 2. Access from iPhone

Open Safari on your iPhone and navigate to:
```
https://identity.web:5001
```

Since your DNS provider redirects `*.web` to localhost, your iPhone will connect to your Mac.

**No certificate warnings!** Since `identity.web` is already in the development certificate's SAN, Safari will trust it immediately.

### 3. Test PRF Extension

Navigate to:
```
https://identity.web:5001/account/prfdemo
```

Click "Authenticate with Passkey" and use Face ID or Touch ID. The PRF extension should now work correctly!

## Why This Works

- ✅ **Valid Domain**: `identity.web` is a valid domain name (WebAuthn requirement)
- ✅ **DNS Resolution**: Your DNS provider redirects `*.web` to localhost
- ✅ **Local Network**: Your iPhone can reach your Mac on the local network
- ✅ **HTTPS**: Required for WebAuthn/Passkeys
- ✅ **Certificate SAN**: `identity.web` is already in the dev certificate (no warnings!)

## Troubleshooting

### Cannot Connect

If your iPhone can't reach `identity.web:5001`:

1. **Verify DNS is working**: Open Safari and try `http://test.web` - it should try to connect
2. **Check same network**: Ensure both devices are on the same WiFi
3. **Check firewall**: Make sure port 5001 is allowed on your Mac

## Testing Checklist

- [ ] Server starts and listens on `https://identity.web:5001`
- [ ] iPhone can access `https://identity.web:5001` in Safari
- [ ] No certificate warnings (thanks to SAN!)
- [ ] Home page loads correctly
- [ ] Can navigate to `/account/prfdemo`
- [ ] "Authenticate with Passkey" button works
- [ ] Face ID/Touch ID prompt appears
- [ ] PRF output is displayed after authentication

## Success!

You should now be able to test passkeys with the PRF extension on your iPhone! 🎉

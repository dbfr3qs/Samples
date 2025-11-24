# iPhone DNS Setup for idp.dev.internal

## Overview

To access `idp.dev.internal` from your iPhone, you need to configure DNS resolution. Since `*.dev.internal` is in the certificate SAN, there will be no certificate warnings!

## Your Mac's IP Address

Your Mac's local IP: `192.168.178.149`

## Option 1: Use DNSCloak or DNS Override App (Easiest)

### Using DNSCloak (Free)

1. Install **DNSCloak** from the App Store
2. Open DNSCloak
3. Go to **Settings** → **Advanced** → **Hosts**
4. Add entry:
   - Host: `idp.dev.internal`
   - IP: `192.168.178.149`
5. Enable DNSCloak

### Using DNS Override (Free)

1. Install **DNS Override** from the App Store
2. Add custom DNS entry for `idp.dev.internal` → `192.168.178.149`
3. Enable the profile

## Option 2: Use a Local DNS Server (More Complex)

### Install dnsmasq on your Mac

```bash
# Install dnsmasq
brew install dnsmasq

# Configure dnsmasq
echo "address=/dev.internal/192.168.178.149" >> /opt/homebrew/etc/dnsmasq.conf

# Start dnsmasq
sudo brew services start dnsmasq
```

### Configure iPhone to use Mac as DNS

1. On iPhone: Settings → Wi-Fi → (i) next to your network
2. Scroll to **DNS**
3. Tap **Configure DNS** → **Manual**
4. Remove existing DNS servers
5. Add DNS Server: `192.168.178.149` (your Mac's IP)
6. Tap **Save**

## Option 3: Edit Hosts File via Shortcuts (iOS 16+)

This is a workaround using iOS Shortcuts, but it's limited and may not work for all scenarios.

## Option 4: Use mDNS/Bonjour (Simplest - Try This First!)

Actually, you might not need any DNS configuration! Try using `.local` instead:

### Update Configuration to Use .local

Change `idp.dev.internal` to `<your-mac-hostname>.local`

First, find your Mac's hostname:
```bash
hostname
```

For example, if it returns `Chris-MacBook-Pro`, you would use:
```
https://Chris-MacBook-Pro.local:5001
```

However, this won't match the certificate SAN, so you'll get a certificate warning.

## Recommended Approach

**Best option:** Use **DNS Override** app on iPhone:

1. Install from App Store (free)
2. Add: `idp.dev.internal` → `192.168.178.149`
3. Enable the profile
4. Navigate to `https://idp.dev.internal:5001` in Safari
5. No certificate warnings! (it's in the SAN)

## Testing

Once DNS is configured:

1. Open Safari on iPhone
2. Navigate to: `https://idp.dev.internal:5001`
3. Should load without certificate warnings
4. Test PRF: `https://idp.dev.internal:5001/account/prfdemo`

## Troubleshooting

### DNS Not Resolving

Test DNS resolution:
```bash
# On iPhone, use a DNS lookup app or try pinging
# Or just try to access http://idp.dev.internal:5001 in Safari
```

### Still Getting Certificate Warnings

- Verify the certificate SAN includes `*.dev.internal`
- Check that you're accessing exactly `idp.dev.internal` (not a typo)

### Can't Install DNS Override App

Alternative: Configure your router's DNS to resolve `*.dev.internal` to your Mac's IP (if your router supports custom DNS entries).

# iPhone DNS Configuration Fix

## Problem
iPhone cannot resolve `api.dev.internal` - DNS queries are failing.

## Root Cause
The iPhone is not configured to use your Mac (`192.168.178.153`) as its DNS server.

## Solution: Configure iPhone DNS

### Step 1: Check Current DNS Settings

On your iPhone:
1. Open **Settings**
2. Tap **Wi-Fi**
3. Tap the **(i)** icon next to your connected network
4. Scroll down to **DNS**

**What do you see?**
- If it shows your router's IP (e.g., `192.168.178.1`) or ISP DNS (e.g., `8.8.8.8`)
- Then the iPhone is NOT using your Mac for DNS

### Step 2: Configure iPhone to Use Mac DNS

1. Still in the Wi-Fi settings, scroll to **DNS**
2. Tap **Configure DNS**
3. Select **Manual**
4. Tap **Remove** on all existing DNS servers (red minus button)
5. Tap **Add Server**
6. Enter: `192.168.178.153` (your Mac's IP)
7. Tap **Save**

### Step 3: Verify DNS Configuration

1. Open **Safari** on iPhone
2. Navigate to: `http://api.dev.internal:5002`
   - Note: Use `http://` first to test DNS only (no certificate)
   - If DNS works, you'll get a connection error (expected - API requires HTTPS)
   - If DNS fails, you'll see "Cannot Find Server" or similar

### Step 4: Test HTTPS (After DNS Works)

Once DNS resolves:
1. Navigate to: `https://api.dev.internal:5002/claims`
2. If you see certificate warning, you need to install the certificate
3. If you see "401 Unauthorized", DNS and certificate are working! ✅

---

## Alternative: Use DNS Override App

If manual DNS configuration doesn't work (some networks block custom DNS):

### Option 1: DNS Override (Free App)

1. Install **DNS Override** from App Store
2. Open the app
3. Tap **+** to add a new rule
4. Enter:
   - **Host**: `api.dev.internal`
   - **IP**: `192.168.178.153`
5. Add another rule:
   - **Host**: `idp.dev.internal`
   - **IP**: `192.168.178.153`
6. Enable the profile
7. Go to Settings → General → VPN & Device Management
8. Install the DNS Override profile

### Option 2: DNSCloak (Free App)

1. Install **DNSCloak** from App Store
2. Open DNSCloak
3. Go to **Settings** → **Advanced** → **Hosts**
4. Add entries:
   - `api.dev.internal` → `192.168.178.153`
   - `idp.dev.internal` → `192.168.178.153`
5. Enable DNSCloak

---

## Troubleshooting

### DNS Still Not Working?

**Check 1: Verify Mac's IP hasn't changed**
```bash
ipconfig getifaddr en0
```
Should return: `192.168.178.153`

If different, update dnsmasq and regenerate certificates.

**Check 2: Test DNS from Mac**
```bash
nslookup api.dev.internal 192.168.178.153
```
Should return: `192.168.178.153`

**Check 3: Verify dnsmasq is running**
```bash
sudo brew services list | grep dnsmasq
```
Should show: `started`

**Check 4: Check Mac firewall**
```bash
# Check if port 53 is accessible
sudo lsof -i :53
```
Should show dnsmasq listening.

**Check 5: Restart dnsmasq**
```bash
sudo brew services restart dnsmasq
```

### Network Blocking DNS?

Some corporate or public WiFi networks block custom DNS servers.

**Test**: Can you access `idp.dev.internal` on iPhone?
- If YES: DNS is working, just add `api.dev.internal`
- If NO: Network may be blocking custom DNS

**Solution**: Use DNS Override app (bypasses network DNS blocking)

### iPhone Caching Old DNS

If you previously tried to access `api.dev.internal`:

1. **Clear Safari cache**:
   - Settings → Safari → Clear History and Website Data
2. **Toggle Airplane Mode**:
   - Settings → Airplane Mode ON → Wait 5 seconds → OFF
3. **Restart iPhone**

---

## Quick Test Commands (Mac)

```bash
# Test DNS from Mac
nslookup api.dev.internal 192.168.178.153

# Check dnsmasq status
sudo brew services list | grep dnsmasq

# Check dnsmasq is listening
sudo lsof -i :53

# View dnsmasq config
tail -10 /opt/homebrew/etc/dnsmasq.conf

# Restart dnsmasq
sudo brew services restart dnsmasq
```

---

## Expected Results

### ✅ DNS Working
- Safari can navigate to `http://api.dev.internal:5002`
- Shows connection error (expected - needs HTTPS)
- No "Cannot Find Server" error

### ✅ DNS + Certificate Working
- Safari can navigate to `https://api.dev.internal:5002/claims`
- Shows "401 Unauthorized" (expected - needs auth token)
- No certificate warnings
- No DNS errors

### ❌ DNS Not Working
- Safari shows "Cannot Find Server"
- Or "Safari cannot open the page"
- This means DNS is not resolving

---

## Next Steps After DNS Works

1. Install certificate: `api-dev-cert.cer` (on Desktop)
2. Trust certificate: Settings → General → About → Certificate Trust Settings
3. Test API: `https://api.dev.internal:5002/claims`
4. Run mobile app

---

## Why This Happens

- **dnsmasq** runs on your Mac and resolves `*.dev.internal` domains
- **iPhone** needs to be told to use your Mac for DNS queries
- By default, iPhone uses router or ISP DNS servers
- Those servers don't know about `*.dev.internal` domains
- Solution: Point iPhone DNS to your Mac's IP

---

## Summary

**The Fix**: Configure iPhone DNS to `192.168.178.153`

**Location**: Settings → Wi-Fi → (i) → Configure DNS → Manual → Add `192.168.178.153`

**Test**: Safari → `http://api.dev.internal:5002` (should connect, not "Cannot Find Server")

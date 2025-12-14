# Fix iPhone DNS Cache Issue

## Problem
- iPhone can resolve `idp.dev.internal` ✅
- iPhone cannot resolve `api.dev.internal` ❌
- Mac can resolve both domains ✅

## Root Cause
The iPhone's DNS cache doesn't have the `api.dev.internal` entry yet. Since `idp.dev.internal` was added earlier, it's cached. The new `api.dev.internal` entry needs to be picked up.

## Quick Fix: Clear iPhone DNS Cache

### Method 1: Toggle Airplane Mode (Fastest)
1. On iPhone: **Settings** → **Airplane Mode** → Toggle **ON**
2. Wait 5 seconds
3. Toggle **OFF**
4. Test: Safari → `http://api.dev.internal:5002`

### Method 2: Restart iPhone
1. Power off iPhone
2. Power on
3. Test: Safari → `http://api.dev.internal:5002`

### Method 3: Renew DHCP Lease
1. **Settings** → **Wi-Fi**
2. Tap **(i)** next to your network
3. Tap **Renew Lease**
4. Tap **Renew Lease** again to confirm
5. Test: Safari → `http://api.dev.internal:5002`

### Method 4: Forget and Rejoin Network
1. **Settings** → **Wi-Fi**
2. Tap **(i)** next to your network
3. Tap **Forget This Network**
4. Rejoin the network
5. Reconfigure DNS to `192.168.178.153` (if needed)
6. Test: Safari → `http://api.dev.internal:5002`

## Verification

### Test DNS Resolution
Open Safari on iPhone and try:

1. **Test idp (should work)**:
   ```
   http://idp.dev.internal:5001
   ```
   Should load the IdentityServer page

2. **Test api (should work after cache clear)**:
   ```
   http://api.dev.internal:5002
   ```
   Should show connection error (expected - needs HTTPS)
   NOT "Cannot Find Server"

3. **Test api with HTTPS**:
   ```
   https://api.dev.internal:5002/claims
   ```
   Should show 401 Unauthorized (after certificate installed)

## Alternative: Use Wildcard DNS

Instead of adding each subdomain individually, you can use a wildcard in dnsmasq.

**Already configured**: The dnsmasq config uses `address=/idp.dev.internal/...` format which should match subdomains.

However, if you want explicit wildcard:
```bash
# On Mac
echo "address=/.dev.internal/192.168.178.153" | sudo tee -a /opt/homebrew/etc/dnsmasq.conf
sudo brew services restart dnsmasq
```

This would match ALL `*.dev.internal` domains.

## Verify from Mac

```bash
# Both should return 192.168.178.153
dig @192.168.178.153 idp.dev.internal +short
dig @192.168.178.153 api.dev.internal +short
```

## If Still Not Working

### Check iPhone DNS Configuration
Settings → Wi-Fi → (i) → DNS

Should show: `192.168.178.153`

If not, reconfigure:
1. Configure DNS → Manual
2. Remove all servers
3. Add: `192.168.178.153`
4. Save

### Test with IP Address Directly
Try accessing the API using the IP:
```
https://192.168.178.153:5002/claims
```

If this works but `api.dev.internal` doesn't, it's definitely a DNS issue.

## Expected Behavior After Fix

✅ `http://api.dev.internal:5002` → Connection error (needs HTTPS)
✅ `https://api.dev.internal:5002/claims` → 401 Unauthorized (needs auth)
❌ "Cannot Find Server" → DNS still not working

## Summary

**Most likely fix**: Toggle Airplane Mode on iPhone to clear DNS cache

**Test**: Safari → `http://api.dev.internal:5002` (should NOT say "Cannot Find Server")

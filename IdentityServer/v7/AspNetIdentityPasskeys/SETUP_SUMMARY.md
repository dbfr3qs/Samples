# Setup Summary - API iPhone Access

## ✅ What's Been Configured

### 1. DNS Resolution (dnsmasq)
- **Status**: ✅ Configured and running
- **Domains**:
  - `idp.dev.internal` → `192.168.178.153`
  - `api.dev.internal` → `192.168.178.153`
- **Verified**: DNS queries return correct IP

### 2. SSL Certificate
- **Status**: ✅ Generated
- **Domain**: `api.dev.internal`
- **SANs**: `api.dev.internal`, `*.dev.internal`, `localhost`, `192.168.178.153`, `127.0.0.1`
- **Location**: 
  - API: `Api/certs/api-dev-cert.pfx`
  - iPhone: `~/Desktop/api-dev-cert.cer` (ready to transfer)

### 3. API Configuration
- **Status**: ✅ Configured
- **URLs**: 
  - `https://api.dev.internal:5002` (for iPhone)
  - `https://localhost:5002` (for Mac)
- **Authority**: `https://idp.dev.internal:5001`
- **Certificate**: Configured in `launchSettings.json`

### 4. Mobile App
- **Status**: ✅ Already configured
- **API URL**: `https://api.dev.internal:5002`
- **No changes needed**

---

## 📱 Next Steps (iPhone Setup)

### Step 1: Transfer Certificate
The certificate is on your Desktop: `api-dev-cert.cer`

**Recommended**: AirDrop to iPhone
1. Right-click `api-dev-cert.cer` on Desktop
2. Share → AirDrop → Send to iPhone

### Step 2: Install Certificate
1. Tap certificate on iPhone
2. Settings → General → VPN & Device Management
3. Tap certificate → Install (3 times) → Done

### Step 3: Trust Certificate
1. Settings → General → About → Certificate Trust Settings
2. Toggle ON "api.dev.internal"
3. Tap Continue

### Step 4: Verify DNS
Settings → Wi-Fi → (i) → DNS should show: `192.168.178.153`

### Step 5: Test
1. Start API: `cd Api && dotnet run`
2. Safari: `https://api.dev.internal:5002/claims`
3. Should see 401 (no cert warnings!)
4. Mobile app should work!

---

## 🚀 Running the System

### Start IdentityServer
```bash
cd IdentityServerAspNetIdentityPasskeys
dotnet run
```
Runs on: `https://idp.dev.internal:5001`

### Start API
```bash
cd Api
dotnet run
```
Runs on: `https://api.dev.internal:5002`

### Test API (Mac)
```bash
curl -k https://localhost:5002/claims
# Expected: 401 Unauthorized
```

### Test DNS (Mac)
```bash
nslookup api.dev.internal 192.168.178.153
# Expected: 192.168.178.153
```

---

## 📁 Files Created

### Scripts
- `generate-api-cert.sh` - Generate SSL certificate
- `export-api-cert-for-iphone.sh` - Export cert to Desktop

### Documentation
- `API_QUICK_START.md` - Quick reference guide
- `API_IPHONE_SETUP.md` - Detailed setup instructions
- `SETUP_SUMMARY.md` - This file

### Certificates
- `Api/certs/api-dev-cert.pfx` - API certificate
- `Api/certs/api-dev-cert.cer` - iPhone certificate
- `Api/certs/api-dev-cert.crt` - Certificate file
- `Api/certs/api-dev-cert.key` - Private key
- `~/Desktop/api-dev-cert.cer` - Ready for iPhone

### Configuration
- `Api/Properties/launchSettings.json` - Updated for api.dev.internal:5002
- `Api/Program.cs` - Updated authority to idp.dev.internal:5001

---

## 🔧 Configuration Details

### dnsmasq Config
Location: `/opt/homebrew/etc/dnsmasq.conf`
```
address=/idp.dev.internal/192.168.178.153
address=/api.dev.internal/192.168.178.153
listen-address=127.0.0.1,192.168.178.153
bind-interfaces
```

### API Launch Settings
```json
{
  "applicationUrl": "https://api.dev.internal:5002;https://localhost:5002",
  "environmentVariables": {
    "ASPNETCORE_Kestrel__Certificates__Default__Path": "certs/api-dev-cert.pfx",
    "ASPNETCORE_Kestrel__Certificates__Default__Password": "dev-password"
  }
}
```

### Mobile App Config
File: `IdpMobileClient/Sources/IdpMobileClient/ApiClient.swift`
```swift
apiBaseURL: String = "https://api.dev.internal:5002"
```

---

## 🐛 Troubleshooting

### DNS Issues
```bash
# Check dnsmasq status
sudo brew services list | grep dnsmasq

# Restart dnsmasq
sudo brew services restart dnsmasq

# Test DNS
nslookup api.dev.internal 192.168.178.153
```

### Certificate Issues
- Verify installation: Settings → General → VPN & Device Management
- Verify trust: Settings → General → About → Certificate Trust Settings
- Restart Safari after trusting

### API Connection Issues
```bash
# Check firewall
sudo /usr/libexec/ApplicationFirewall/socketfilterfw --listapps | grep dotnet

# Allow dotnet
sudo /usr/libexec/ApplicationFirewall/socketfilterfw --unblockapp /usr/local/share/dotnet/dotnet
```

### IP Address Changed
If Mac IP changes:
1. Update `/opt/homebrew/etc/dnsmasq.conf`
2. Run `./generate-api-cert.sh`
3. Run `./export-api-cert-for-iphone.sh`
4. Reinstall certificate on iPhone
5. Restart dnsmasq: `sudo brew services restart dnsmasq`

---

## ✅ Verification Checklist

- [x] dnsmasq configured with api.dev.internal
- [x] dnsmasq running successfully
- [x] DNS resolution working (verified with nslookup)
- [x] SSL certificate generated with correct SANs
- [x] Certificate exported to Desktop
- [x] API configured to use certificate
- [x] API configured to listen on api.dev.internal:5002
- [x] API authority updated to idp.dev.internal:5001
- [x] Mobile app already configured for api.dev.internal:5002
- [ ] Certificate installed on iPhone (manual step)
- [ ] Certificate trusted on iPhone (manual step)
- [ ] iPhone DNS configured (manual step)
- [ ] Tested from iPhone Safari (manual step)
- [ ] Tested from mobile app (manual step)

---

## 📚 Documentation

- **Quick Start**: `API_QUICK_START.md` - Fast setup guide
- **Detailed Guide**: `API_IPHONE_SETUP.md` - Complete instructions
- **This File**: `SETUP_SUMMARY.md` - Configuration overview

---

## 🔐 Security Notes

⚠️ **Development Only**: Self-signed certificates for local testing only

⚠️ **Certificate Expiry**: Valid for 1 year from generation date

⚠️ **Network Access**: Anyone on local network can access while running

---

## 📞 Support

If issues persist:
1. Check `API_IPHONE_SETUP.md` troubleshooting section
2. Verify all checklist items above
3. Check dnsmasq logs: `sudo brew services list`
4. Test DNS and API from Mac first before testing from iPhone

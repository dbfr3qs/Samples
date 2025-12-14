# API Quick Start Guide

## TL;DR - Get the API Working on iPhone

### 1. Transfer Certificate to iPhone
The certificate is already on your Desktop: `api-dev-cert.cer`

**AirDrop it to your iPhone** (easiest method)

### 2. Install Certificate on iPhone
1. Tap the certificate file on iPhone
2. Settings → General → VPN & Device Management
3. Tap the certificate → Install → Install → Install → Done

### 3. Trust the Certificate
1. Settings → General → About → Certificate Trust Settings
2. Toggle ON "api.dev.internal"
3. Tap Continue

### 4. Verify iPhone DNS
Settings → Wi-Fi → (i) → DNS should show: `192.168.178.153`

If not:
- Configure DNS → Manual
- Add: `192.168.178.153`
- Save

### 5. Start the API
```bash
cd Api
dotnet run
```

### 6. Test from iPhone Safari
Open: `https://api.dev.internal:5002/claims`

Should see: 401 Unauthorized (this is correct - endpoint requires auth)
No certificate warnings!

### 7. Test from Mobile App
Open the mobile app → Sign in with passkey → Call API

Should work without errors!

---

## What Was Configured

### DNS (dnsmasq)
- `api.dev.internal` → `192.168.178.153`
- `idp.dev.internal` → `192.168.178.153`

### Certificate
- Domain: `api.dev.internal`
- SANs: `api.dev.internal`, `*.dev.internal`, `localhost`, IPs
- Location: `Api/certs/api-dev-cert.pfx` (for API)
- Location: `~/Desktop/api-dev-cert.cer` (for iPhone)

### API Configuration
- Listens on: `https://api.dev.internal:5002` and `https://localhost:5002`
- Authority: `https://idp.dev.internal:5001`
- Certificate: `certs/api-dev-cert.pfx`

### Mobile App
- Already configured to use: `https://api.dev.internal:5002`
- No changes needed!

---

## Troubleshooting

### DNS not working?
```bash
# Test DNS
nslookup api.dev.internal 192.168.178.153

# Should return: 192.168.178.153
```

### Certificate not trusted?
- Make sure you completed step 3 (Certificate Trust Settings)
- Restart Safari

### API not accessible?
```bash
# Test from Mac
curl -k https://localhost:5002/claims

# Should return: 401 Unauthorized
```

### Need to regenerate certificate?
```bash
./generate-api-cert.sh
./export-api-cert-for-iphone.sh
# Then reinstall on iPhone
```

---

## Full Documentation
See `API_IPHONE_SETUP.md` for detailed instructions and troubleshooting.

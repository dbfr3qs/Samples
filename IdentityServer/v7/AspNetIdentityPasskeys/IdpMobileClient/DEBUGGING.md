# Debugging Guide

## Viewing Logs in Xcode

1. **Open Debug Console**: View → Debug Area → Show Debug Area (⌘⇧Y)
2. **Run the app** and trigger the error
3. **Look for log messages** with these prefixes:
   - 🔐 `[Passkey]` - Passkey authentication flow
   - 🌐 `[PasskeyService]` - Network requests to IdP
   - 📡 Response status codes
   - ❌ Error messages
   - ✅ Success messages

## Common Issues and Solutions

### 1. Network Connection Errors

**Symptoms**: `URLError`, connection refused, timeout

**Logs to check**:
```
🌐 [PasskeyService] POST https://idp.dev.internal:5001/api/passkey/authenticate/begin
❌ [PasskeyService] Authentication failed with status XXX
```

**Solutions**:
- Verify IdP is running: `curl https://idp.dev.internal:5001/.well-known/openid-configuration`
- Check `/etc/hosts` has: `127.0.0.1 idp.dev.internal`
- Verify SSL certificate is installed: `./install-cert-to-simulator.sh`

### 2. SSL Certificate Errors

**Symptoms**: `NSURLErrorServerCertificateUntrusted`, SSL handshake failed

**Solutions**:
```bash
# Reinstall certificate
./install-cert-to-simulator.sh

# Verify certificate is installed
xcrun simctl keychain booted list
```

### 3. IdP Endpoint Not Found (404)

**Symptoms**: Response status 404

**Solutions**:
- Check if IdP has passkey endpoints:
  - `/api/passkey/authenticate/begin`
  - `/api/passkey/authenticate/complete`
- Verify IdP configuration supports WebAuthn
- Check IdP logs for errors

### 4. Invalid Response Format

**Symptoms**: `DecodingError`, failed to decode response

**Logs to check**:
```
❌ [PasskeyService] Failed to decode response: ...
📄 [PasskeyService] Response body: {...}
```

**Solutions**:
- Check the response body in logs
- Verify IdP returns correct JSON format:
  ```json
  {
    "challenge": "base64url-string",
    "timeout": 60000,
    "rpId": "idp.dev.internal"
  }
  ```

### 5. Passkey Not Available

**Symptoms**: `PasskeyError error 1`, operation couldn't be completed

**Solutions**:
- Ensure iOS 15+ simulator/device
- Check relying party identifier matches: `idp.dev.internal`
- Verify user has registered a passkey (may need to register first)

## Manual Testing

### Test IdP Endpoint

```bash
# Test authentication begin endpoint
curl -X POST https://idp.dev.internal:5001/api/passkey/authenticate/begin \
  -H "Content-Type: application/json" \
  -d '{}' \
  -v

# Should return:
# {
#   "challenge": "...",
#   "timeout": 60000,
#   "rpId": "idp.dev.internal"
# }
```

### Test DNS Resolution

```bash
# Check DNS
ping idp.dev.internal

# Should resolve to 127.0.0.1
```

### Test SSL Certificate

```bash
# Test HTTPS connection
curl -v https://idp.dev.internal:5001/.well-known/openid-configuration

# Should NOT show certificate errors
```

## Enable Network Logging

Add to your scheme in Xcode:

1. Product → Scheme → Edit Scheme
2. Run → Arguments → Environment Variables
3. Add: `CFNETWORK_DIAGNOSTICS = 3`

This will show detailed network logs including SSL handshake.

## Check Simulator State

```bash
# List booted simulators
xcrun simctl list devices | grep Booted

# Check keychain certificates
xcrun simctl keychain booted list

# Reset simulator (WARNING: deletes all data)
xcrun simctl erase booted
```

## Common Error Codes

| Error | Meaning | Solution |
|-------|---------|----------|
| URLError -1200 | SSL certificate error | Install certificate |
| URLError -1004 | Could not connect to server | Check IdP is running |
| URLError -1001 | Request timeout | Check network/firewall |
| 404 | Endpoint not found | Verify IdP endpoints |
| 500 | Server error | Check IdP logs |

## Debugging Checklist

- [ ] IdP is running on port 5001
- [ ] DNS resolves `idp.dev.internal` to 127.0.0.1
- [ ] SSL certificate installed in simulator
- [ ] Simulator restarted after cert install
- [ ] IdP has WebAuthn endpoints configured
- [ ] Xcode console shows detailed logs
- [ ] No certificate errors in Safari (test in simulator)

## Getting More Help

If you're still stuck:

1. **Copy the Xcode console logs** (all lines with 🔐, 🌐, ❌)
2. **Check IdP logs** for corresponding errors
3. **Test endpoints manually** with curl
4. **Verify IdP configuration** matches client settings

## Useful Commands

```bash
# Tail IdP logs (if using dotnet)
dotnet run --project /path/to/IdP | grep -i passkey

# Watch simulator logs
xcrun simctl spawn booted log stream --predicate 'processImagePath contains "IdpMobileDemoApp"'

# Test with verbose curl
curl -v -X POST https://idp.dev.internal:5001/api/passkey/authenticate/begin \
  -H "Content-Type: application/json" \
  -d '{}' 2>&1 | tee curl-debug.log
```

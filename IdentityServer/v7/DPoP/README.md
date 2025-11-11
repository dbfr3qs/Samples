# OAuth 2.0 DPoP Attack Demonstration

This repository demonstrates a browser-swapping attack against OAuth 2.0 with DPoP (Demonstrating Proof of Possession). The attack exploits compromised JavaScript in a victim's browser to steal authorization codes, bypassing DPoP, PKCE, and even PAR (Pushed Authorization Requests).

## 🚀 Quick Start

**Want to see the attack in action? Start here:**

1. Start all services:
   ```bash
   ./start-all.sh
   ```

2. Follow the step-by-step guide:
   - **[PORTABLE_QUICK_START.md](PORTABLE_QUICK_START.md)** - 5-minute demo

## 📚 Documentation

### For Security Researchers

- **[PORTABLE_ATTACK_GUIDE.md](PORTABLE_ATTACK_GUIDE.md)** - Complete guide to the portable attack tool
  - Works with any OAuth 2.0/OIDC client
  - Detailed attack flow explanation
  - Mitigations and defenses
  - Educational value and limitations

### Technical Analysis

- **[PAR_ATTACK_ANALYSIS.md](PAR_ATTACK_ANALYSIS.md)** - Why PAR doesn't prevent this attack
  - Detailed analysis of PAR security properties
  - Why `request_uri` exposure is problematic
  - Comparison with standard OAuth flow

- **[ATTACK_DEMO_README.md](ATTACK_DEMO_README.md)** - Original attack documentation
  - Legacy version (WebClient-specific)
  - Still useful for understanding the core concepts

## 🏗️ Architecture

### Components

```
┌─────────────────┐
│ IdentityServer  │  Port 5001 - Authorization Server
│      Host       │  (Issues tokens, validates DPoP)
└─────────────────┘
        ↑
        │ OAuth 2.0 + DPoP
        │
┌─────────────────┐
│   WebClient     │  Port 5010 - Example OAuth Client
│  (Target App)   │  (Has malicious JS on /Home/Secure)
└─────────────────┘
        ↑
        │ DPoP-bound tokens
        │
┌─────────────────┐
│      API        │  Port 5005 - Protected Resource
│                 │  (Validates DPoP-bound access tokens)
└─────────────────┘

┌─────────────────┐
│  AttackerApi    │  Port 7666 - Attack Coordination
│   (Attacker)    │  (Stores stolen credentials)
└─────────────────┘
```

### Attack Flow

```
1. Attacker                    2. Victim                    3. Attacker
   ↓                              ↓                            ↓
Start OAuth flow            Malicious JS polls          Complete OAuth flow
Copy authorize URL          AttackerApi for params      with stolen code
   ↓                              ↓                            ↓
Paste into AttackerApi      Perform silent OAuth        Gain access to
Submit parameters           Intercept auth code         victim's account
   ↓                              ↓                            ↓
Wait for code...            Send code to AttackerApi    SUCCESS!
```

## 🎯 What Makes This Attack Unique?

### Portable Design

Unlike the original demo, this version:
- ✅ Works with **any OAuth 2.0/OIDC client** (not just the example WebClient)
- ✅ Standalone AttackerApi UI (no need to modify target app's pages)
- ✅ Manual parameter extraction (more realistic attack scenario)
- ✅ Can be tested against real-world applications

### Bypasses Multiple Security Mechanisms

This attack works even when the target uses:
- ✅ **DPoP (RFC 9449)** - Demonstrating Proof of Possession
- ✅ **PKCE (RFC 7636)** - Proof Key for Code Exchange
- ✅ **PAR (RFC 9126)** - Pushed Authorization Requests
- ✅ **Nonce validation** - OpenID Connect replay protection
- ✅ **State parameter** - CSRF protection

### Why It Works

The attack exploits a fundamental assumption in OAuth 2.0:
- **Authorization codes are bearer tokens** (anyone with the code can use it)
- **DPoP only binds tokens, not codes** (dpop_jkt is just a hint during authorize)
- **response_mode=fragment keeps codes client-side** (JavaScript can intercept)
- **No session binding** (code isn't tied to the browser session)

## 🛡️ Mitigations

### Primary Defense: Prevent JavaScript Compromise

The attack **requires** malicious JavaScript in the victim's browser. Prevent this with:

1. **Content Security Policy (CSP)**
   ```
   Content-Security-Policy: script-src 'self'; default-src 'self'
   ```

2. **Subresource Integrity (SRI)**
   ```html
   <script src="app.js" integrity="sha384-..." crossorigin="anonymous"></script>
   ```

3. **Regular Security Audits**
   - Review all third-party scripts
   - Monitor for XSS vulnerabilities
   - Use dependency scanning tools

### Additional Defenses

- **Short authorization code lifetime** (30 seconds instead of 10 minutes)
- **Device flow for high-risk scenarios** (out-of-band confirmation)
- **Step-up authentication** (additional auth for sensitive operations)
- **Session binding** (experimental, not in OAuth spec)

## 📖 Educational Value

This demonstration teaches:

1. **OAuth 2.0 complexity** - Even with modern extensions, subtle vulnerabilities exist
2. **Defense in depth** - No single security mechanism is sufficient
3. **JavaScript security** - Compromised JS can bypass many OAuth protections
4. **Browser security model** - Understanding fragments, iframes, and same-origin policy

## ⚠️ Legal and Ethical Considerations

**This tool is for educational and authorized security testing only.**

- ❌ Do NOT use against applications you don't own
- ❌ Do NOT use without explicit permission
- ✅ Only test your own applications or with written authorization
- ✅ Follow responsible disclosure practices

Unauthorized access to computer systems is illegal in most jurisdictions.

## 🔧 Development

### Project Structure

```
DPoP/
├── IdentityServerHost/     # Authorization Server (Duende IdentityServer)
├── WebClient/              # Example OAuth client (ASP.NET Core MVC)
├── Api/                    # Protected resource server
├── AttackerApi/            # Attack coordination server
│   └── wwwroot/
│       ├── index.html      # Attacker UI (NEW - portable version)
│       ├── malicious-attack.js  # Victim-side script (NEW - portable)
│       └── attack.js       # Legacy attack script
├── PORTABLE_ATTACK_GUIDE.md     # Main documentation (NEW)
├── PORTABLE_QUICK_START.md      # Quick start guide (NEW)
├── PAR_ATTACK_ANALYSIS.md       # PAR security analysis
└── start-all.sh            # Start all services
```

### Running Individual Services

```bash
# Authorization Server
cd IdentityServerHost && dotnet run

# OAuth Client
cd WebClient && dotnet run

# Protected API
cd Api && dotnet run

# Attacker API
cd AttackerApi && dotnet run
```

### Configuration

All services use HTTPS with self-signed certificates:
- Accept certificate warnings during development
- Or install the development certificates: `dotnet dev-certs https --trust`

## 📚 References

- [RFC 9449: OAuth 2.0 Demonstrating Proof of Possession (DPoP)](https://datatracker.ietf.org/doc/html/rfc9449)
- [RFC 7636: Proof Key for Code Exchange (PKCE)](https://datatracker.ietf.org/doc/html/rfc7636)
- [RFC 9126: OAuth 2.0 Pushed Authorization Requests (PAR)](https://datatracker.ietf.org/doc/html/rfc9126)
- [OAuth 2.0 Security Best Current Practice](https://datatracker.ietf.org/doc/html/draft-ietf-oauth-security-topics)
- [Duende IdentityServer Documentation](https://docs.duendesoftware.com/)

## 🤝 Contributing

This is an educational security demonstration. Contributions that improve:
- Documentation clarity
- Attack realism
- Mitigation examples
- Educational value

are welcome!

## 📝 License

This project is for educational purposes. Use responsibly and ethically.

## 🙏 Acknowledgments

This demonstration is built on:
- [Duende IdentityServer](https://duendesoftware.com/products/identityserver) - OAuth 2.0 and OpenID Connect framework
- [ASP.NET Core](https://dotnet.microsoft.com/apps/aspnet) - Web application framework
- OAuth 2.0 and OpenID Connect specifications

## 📧 Contact

For questions about this demonstration:
- Review the documentation in this repository
- Check the browser console for debugging information
- Ensure all services are running on the correct ports

---

**Remember**: This is a security research tool. Use only for educational purposes and authorized testing.

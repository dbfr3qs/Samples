# Native iOS Passkey Implementation Plan for IdentityServer

## Executive Summary

This document outlines a comprehensive plan to implement full FIDO2/WebAuthn specification-compliant passkey authentication for native iOS clients in IdentityServer. The current implementation uses ASP.NET Identity's passkey support, which assumes browser-based clients and **skips cryptographic validation** for native clients.

## Current State Analysis

### What Works ✅
- Passkey registration/authentication endpoints
- Challenge generation and session storage
- Credential storage in database
- OAuth/OIDC integration with PKCE
- iOS app creates passkeys via ASAuthorizationController

### Critical Security Gaps ❌

**Location:** `MobilePasskeyEndpoints.cs:296-301`

```csharp
// TODO: In production, you should perform full WebAuthn assertion validation here
// For now, we trust that:
// 1. The credential exists in the database
// 2. The challenge matches
// 3. The origin is correct (validated by IdentityPasskeyOptions)
// 4. The user owns this credential
```

**Missing Validations:**
1. ❌ **No cryptographic signature verification**
2. ❌ **No authenticator data validation** (flags, counter, extensions)
3. ❌ **No attestation verification** during registration
4. ❌ **Replay attack vulnerability** (counter not validated)
5. ❌ **Incomplete client data validation**

**Root Cause:** ASP.NET Identity's `SignInManager.PerformPasskeyAttestationAsync()` assumes browser-based WebAuthn. Native iOS sends raw credential data requiring manual parsing and validation.

---

## Implementation Plan

### Phase 1: Foundation (Week 1)

#### 1.1 Add Fido2-Net-Lib

```bash
cd IdentityServerAspNetIdentityPasskeys
dotnet add package Fido2.NetFramework
```

**Library:** https://github.com/passwordless-lib/fido2-net-lib

#### 1.2 Configure Fido2 Service

```csharp
// In HostingExtensions.cs
builder.Services.AddSingleton<IFido2>(sp => {
    return new Fido2(new Fido2Configuration {
        ServerDomain = "idp.dev.internal",
        ServerName = "Identity Server",
        Origins = new HashSet<string> {
            "https://idp.dev.internal",
            "https://idp.dev.internal:5001",
            "ios:bundle-id://com.idp.mobile"  // Native iOS
        },
        TimestampDriftTolerance = 60000
    });
});

builder.Services.AddScoped<IChallengeStore, ChallengeStore>();
builder.Services.AddScoped<ICredentialStore, CredentialStore>();
builder.Services.AddSingleton<NativeOriginValidator>();
builder.Services.AddHostedService<ChallengeCleanupService>();
```

#### 1.3 Database Migrations

**Add to AspNetUserPasskeys table:**
```sql
ALTER TABLE AspNetUserPasskeys ADD COLUMN SignatureCounter INTEGER NOT NULL DEFAULT 0;
ALTER TABLE AspNetUserPasskeys ADD COLUMN AttestationFormat TEXT;
ALTER TABLE AspNetUserPasskeys ADD COLUMN AaGuid BLOB;
ALTER TABLE AspNetUserPasskeys ADD COLUMN LastUsed DATETIME;
ALTER TABLE AspNetUserPasskeys ADD COLUMN DeviceType TEXT;
```

**New PasskeyChallenges table:**
```sql
CREATE TABLE PasskeyChallenges (
    Id TEXT PRIMARY KEY,
    Challenge BLOB NOT NULL,
    UserId TEXT,
    ClientType TEXT NOT NULL,
    CreatedAt DATETIME NOT NULL,
    ExpiresAt DATETIME NOT NULL,
    Used BOOLEAN NOT NULL DEFAULT 0
);
CREATE INDEX idx_challenges_expires ON PasskeyChallenges(ExpiresAt);
```

---

### Phase 2: Registration Flow (Week 2)

#### 2.1 Enhanced Registration Begin

**New Endpoint:** `POST /api/passkey/native/register/begin`

**Key Implementation:**

```csharp
apiGroup.MapPost("/native/register/begin", async (
    [FromServices] IFido2 fido2,
    [FromServices] IChallengeStore challengeStore,
    [FromServices] ICredentialStore credentialStore,
    [FromServices] UserManager<ApplicationUser> userManager,
    [FromBody] BeginRegistrationRequest request) =>
{
    var user = await userManager.FindByNameAsync(request.Username);
    if (user == null) {
        user = new ApplicationUser { UserName = request.Username, Email = request.Email };
        await userManager.CreateAsync(user);
    }

    // Get existing credentials to exclude
    var existingCreds = await credentialStore.GetByUserIdAsync(user.Id);
    
    // Create FIDO2 credential creation options
    var options = fido2.RequestNewCredential(
        new Fido2User {
            Id = Encoding.UTF8.GetBytes(user.Id),
            Name = user.UserName,
            DisplayName = request.DisplayName ?? user.UserName
        },
        existingCreds.Select(c => new PublicKeyCredentialDescriptor(c.CredentialId)).ToList(),
        new AuthenticatorSelection {
            AuthenticatorAttachment = AuthenticatorAttachment.Platform,
            RequireResidentKey = true,
            UserVerification = UserVerificationRequirement.Required
        },
        AttestationConveyancePreference.Direct
    );

    // Store challenge
    var challengeId = await challengeStore.StoreAsync(new ChallengeData {
        Challenge = options.Challenge,
        UserId = user.Id,
        ClientType = "native-ios",
        ExpiresAt = DateTime.UtcNow.AddMinutes(5)
    });

    return Results.Ok(new {
        challenge = Base64Url.Encode(options.Challenge),
        rp = new { id = options.Rp.Id, name = options.Rp.Name },
        user = new {
            id = Base64Url.Encode(Encoding.UTF8.GetBytes(user.Id)),
            name = user.UserName,
            displayName = request.DisplayName ?? user.UserName
        },
        pubKeyCredParams = options.PubKeyCredParams,
        timeout = options.Timeout,
        authenticatorSelection = options.AuthenticatorSelection,
        attestation = "direct",
        challengeId = challengeId
    });
});
```

#### 2.2 Enhanced Registration Complete

**Endpoint:** `POST /api/passkey/native/register/complete`

**Key Implementation:**

```csharp
apiGroup.MapPost("/native/register/complete", async (
    [FromServices] IFido2 fido2,
    [FromServices] IChallengeStore challengeStore,
    [FromServices] ICredentialStore credentialStore,
    [FromServices] UserManager<ApplicationUser> userManager,
    [FromServices] NativeOriginValidator originValidator,
    [FromBody] CompleteRegistrationRequest request) =>
{
    // 1. Retrieve challenge
    var challengeData = await challengeStore.GetAndRemoveAsync(request.ChallengeId);
    if (challengeData == null || challengeData.IsExpired) {
        return Results.BadRequest(new { error = "Challenge expired" });
    }

    // 2. Parse credential
    var credential = JsonSerializer.Deserialize<AuthenticatorAttestationRawResponse>(
        request.CredentialJson
    );

    // 3. Prepare verification options
    var options = new CredentialCreateOptions {
        Challenge = challengeData.Challenge,
        Rp = new PublicKeyCredentialRpEntity("idp.dev.internal", "Identity Server", null),
        User = new Fido2User { Id = Encoding.UTF8.GetBytes(challengeData.UserId) },
        PubKeyCredParams = new List<PubKeyCredParam> {
            new() { Type = "public-key", Alg = -7 },  // ES256
            new() { Type = "public-key", Alg = -257 } // RS256
        },
        Timeout = 60000,
        Attestation = AttestationConveyancePreference.Direct,
        AuthenticatorSelection = new AuthenticatorSelection {
            AuthenticatorAttachment = AuthenticatorAttachment.Platform,
            RequireResidentKey = true,
            UserVerification = UserVerificationRequirement.Required
        }
    };

    // 4. ✅ PERFORM FULL ATTESTATION VERIFICATION
    var result = await fido2.MakeNewCredentialAsync(
        credential,
        options,
        originValidator.IsOriginValid
    );

    if (result.Status != "ok") {
        return Results.BadRequest(new { 
            error = "Attestation verification failed",
            detail = result.ErrorMessage 
        });
    }

    // 5. Validate user verification
    if (!result.Result.User.Verified) {
        return Results.BadRequest(new { error = "User verification required" });
    }

    // 6. Store credential
    await credentialStore.AddAsync(new StoredCredential {
        UserId = challengeData.UserId,
        CredentialId = result.Result.CredentialId,
        PublicKey = result.Result.PublicKey,
        SignatureCounter = result.Result.Counter,
        CredType = result.Result.CredType,
        AaGuid = result.Result.Aaguid,
        AttestationFormat = result.Result.AttestationFormat,
        DeviceType = "iOS Platform Authenticator",
        CreatedAt = DateTime.UtcNow
    });

    return Results.Ok(new { success = true });
});
```

**Validation Checklist:**
- ✅ Challenge matches stored value
- ✅ Origin is valid iOS bundle ID
- ✅ RP ID matches expected value
- ✅ User verification performed
- ✅ **Attestation signature cryptographically verified**
- ✅ Public key algorithm supported
- ✅ Credential ID is unique

---

### Phase 3: Authentication Flow (Week 3)

#### 3.1 Enhanced Authentication Begin

**Endpoint:** `POST /api/passkey/native/authenticate/begin`

```csharp
apiGroup.MapPost("/native/authenticate/begin", async (
    [FromServices] IFido2 fido2,
    [FromServices] IChallengeStore challengeStore,
    [FromBody] BeginAuthenticationRequest request) =>
{
    // Generate assertion options
    var options = fido2.GetAssertionOptions(
        allowedCredentials: null, // Support discoverable credentials
        UserVerificationRequirement.Required
    );

    // Store challenge
    var challengeId = await challengeStore.StoreAsync(new ChallengeData {
        Challenge = options.Challenge,
        ClientType = "native-ios",
        ExpiresAt = DateTime.UtcNow.AddMinutes(5)
    });

    return Results.Ok(new {
        challenge = Base64Url.Encode(options.Challenge),
        timeout = options.Timeout,
        rpId = options.RpId,
        userVerification = "required",
        challengeId = challengeId
    });
});
```

#### 3.2 Enhanced Authentication Complete

**Endpoint:** `POST /api/passkey/native/authenticate/complete`

**Key Implementation:**

```csharp
apiGroup.MapPost("/native/authenticate/complete", async (
    [FromServices] IFido2 fido2,
    [FromServices] IChallengeStore challengeStore,
    [FromServices] ICredentialStore credentialStore,
    [FromServices] SignInManager<ApplicationUser> signInManager,
    [FromServices] UserManager<ApplicationUser> userManager,
    [FromServices] IAuthorizationCodeStore codeStore,
    [FromServices] NativeOriginValidator originValidator,
    [FromBody] CompleteAuthenticationRequest request) =>
{
    // 1. Retrieve challenge
    var challengeData = await challengeStore.GetAndRemoveAsync(request.ChallengeId);
    if (challengeData == null || challengeData.IsExpired) {
        return Results.BadRequest(new { error = "Challenge expired" });
    }

    // 2. Parse assertion
    var assertion = JsonSerializer.Deserialize<AuthenticatorAssertionRawResponse>(
        request.CredentialJson
    );

    // 3. Lookup credential
    var storedCredential = await credentialStore.GetByCredentialIdAsync(assertion.RawId);
    if (storedCredential == null) {
        return Results.BadRequest(new { error = "Credential not found" });
    }

    var user = await userManager.FindByIdAsync(storedCredential.UserId);
    if (user == null) {
        return Results.BadRequest(new { error = "User not found" });
    }

    // 4. Prepare assertion options
    var options = new AssertionOptions {
        Challenge = challengeData.Challenge,
        RpId = "idp.dev.internal",
        AllowCredentials = new[] { 
            new PublicKeyCredentialDescriptor(storedCredential.CredentialId) 
        },
        UserVerification = UserVerificationRequirement.Required
    };

    // 5. ✅ PERFORM FULL CRYPTOGRAPHIC SIGNATURE VERIFICATION
    var result = await fido2.MakeAssertionAsync(
        assertion,
        options,
        storedCredential.PublicKey,
        storedCredential.SignatureCounter,
        originValidator.IsOriginValid
    );

    if (result.Status != "ok") {
        Console.WriteLine($"[ERROR] Signature verification failed: {result.ErrorMessage}");
        return Results.BadRequest(new { error = "Signature verification failed" });
    }

    // 6. ✅ VALIDATE COUNTER INCREMENT (REPLAY PROTECTION)
    if (result.Counter <= storedCredential.SignatureCounter) {
        Console.WriteLine($"[SECURITY] Counter anomaly detected!");
        Console.WriteLine($"[SECURITY] Stored: {storedCredential.SignatureCounter}, Received: {result.Counter}");
        return Results.BadRequest(new { error = "Authentication failed" });
    }

    // 7. Update counter
    storedCredential.SignatureCounter = result.Counter;
    storedCredential.LastUsed = DateTime.UtcNow;
    await credentialStore.UpdateAsync(storedCredential);

    // 8. Sign in user
    await signInManager.SignInAsync(user, isPersistent: false);

    // 9. Generate OAuth authorization code
    var code = new AuthorizationCode {
        ClientId = "mobile-client",
        Subject = context.User,
        CreationTime = DateTime.UtcNow,
        Lifetime = 300,
        RedirectUri = "com.idp.mobile://callback",
        RequestedScopes = new[] { "openid", "profile", "api" },
        CodeChallenge = request.CodeChallenge.Sha256(),
        CodeChallengeMethod = request.CodeChallengeMethod ?? "S256",
        IsOpenId = true
    };

    var codeValue = await codeStore.StoreAuthorizationCodeAsync(code);

    return Results.Ok(new { code = codeValue, state = request.State });
});
```

**Validation Checklist:**
- ✅ Challenge matches stored value
- ✅ Origin is valid iOS bundle ID
- ✅ RP ID matches expected value
- ✅ **Signature cryptographically verified**
- ✅ User verification performed
- ✅ **Counter incremented (replay protection)**
- ✅ Credential belongs to authenticated user

---

### Phase 4: Origin Validation

**File:** `Services/NativeOriginValidator.cs`

```csharp
public class NativeOriginValidator
{
    private readonly HashSet<string> _allowedBundleIds = new() {
        "com.idp.mobile",
        "com.idp.mobiledemo"
    };
    
    private readonly HashSet<string> _allowedWebOrigins = new() {
        "https://idp.dev.internal",
        "https://idp.dev.internal:5001"
    };
    
    public bool IsOriginValid(string origin)
    {
        // Web origin
        if (origin.StartsWith("https://")) {
            return _allowedWebOrigins.Contains(origin);
        }
        
        // iOS origin format: ios:bundle-id://com.example.app
        if (origin.StartsWith("ios:bundle-id://")) {
            var bundleId = origin.Substring("ios:bundle-id://".Length);
            return _allowedBundleIds.Contains(bundleId);
        }
        
        return false;
    }
}
```

**iOS Client Data Format:**
```json
{
  "type": "webauthn.create",
  "challenge": "base64url-challenge",
  "origin": "ios:bundle-id://com.idp.mobile"
}
```

---

### Phase 5: Service Interfaces

```csharp
public interface IChallengeStore
{
    Task<string> StoreAsync(ChallengeData challenge);
    Task<ChallengeData?> GetAndRemoveAsync(string challengeId);
    Task CleanupExpiredAsync();
}

public interface ICredentialStore
{
    Task AddAsync(StoredCredential credential);
    Task<StoredCredential?> GetByCredentialIdAsync(byte[] credentialId);
    Task<List<StoredCredential>> GetByUserIdAsync(string userId);
    Task UpdateAsync(StoredCredential credential);
}

public class ChallengeData
{
    public byte[] Challenge { get; set; }
    public string? UserId { get; set; }
    public string ClientType { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime ExpiresAt { get; set; }
    public bool IsExpired => DateTime.UtcNow > ExpiresAt;
}

public class StoredCredential
{
    public string UserId { get; set; }
    public byte[] CredentialId { get; set; }
    public byte[] PublicKey { get; set; }
    public uint SignatureCounter { get; set; }
    public string CredType { get; set; }
    public Guid AaGuid { get; set; }
    public string AttestationFormat { get; set; }
    public string DeviceType { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime? LastUsed { get; set; }
}
```

---

### Phase 6: Testing Strategy (Week 4)

#### Security Tests

```csharp
[Fact]
public async Task Authentication_ReplayAttack_ShouldFail()
{
    // Authenticate once successfully
    var result1 = await AuthenticateAsync(credential);
    Assert.True(result1.Success);
    
    // Try to reuse same assertion
    var result2 = await AuthenticateAsync(credential);
    Assert.False(result2.Success);
}

[Fact]
public async Task Authentication_CounterRollback_ShouldFail()
{
    // Authenticate with counter = 5
    await AuthenticateAsync(credentialWithCounter5);
    
    // Try with counter = 3 (rollback)
    var result = await AuthenticateAsync(credentialWithCounter3);
    Assert.False(result.Success);
}

[Fact]
public async Task Authentication_InvalidSignature_ShouldFail()
{
    var credential = CreateCredentialWithInvalidSignature();
    var result = await AuthenticateAsync(credential);
    Assert.False(result.Success);
}
```

---

## Implementation Roadmap

### Week 1: Foundation
- Add Fido2-Net-Lib package
- Create database migrations
- Implement ChallengeStore and CredentialStore
- Create NativeOriginValidator
- Add background cleanup service

### Week 2: Registration
- Implement `/native/register/begin` endpoint
- Implement `/native/register/complete` with full attestation verification
- Write unit tests
- Test with iOS app

### Week 3: Authentication
- Implement `/native/authenticate/begin` endpoint
- Implement `/native/authenticate/complete` with signature verification
- Add counter validation
- Write unit tests
- Test with iOS app

### Week 4: Testing & Deployment
- Integration tests
- Security tests (replay, counter rollback)
- Performance testing
- Deploy to staging
- Production deployment

---

## Key Security Improvements

### Before (Current State)
```csharp
// ❌ No cryptographic validation
// Just checks: credential exists, challenge matches, user owns credential
await signInManager.SignInAsync(user, isPersistent: false);
```

### After (With Fido2-Net-Lib)
```csharp
// ✅ Full cryptographic validation
var result = await fido2.MakeAssertionAsync(
    assertion,
    options,
    storedCredential.PublicKey,      // Verify signature with public key
    storedCredential.SignatureCounter, // Check counter increment
    IsOriginValid                     // Validate iOS bundle ID
);

if (result.Status != "ok") {
    throw new Fido2VerificationException("Signature verification failed");
}

if (result.Counter <= storedCredential.SignatureCounter) {
    throw new Fido2VerificationException("Replay attack detected");
}
```

---

## Summary

This plan transforms your passkey implementation from **basic credential checking** to **full FIDO2/WebAuthn specification compliance** with:

1. ✅ **Cryptographic signature verification** - Proves possession of private key
2. ✅ **Attestation verification** - Validates authenticator during registration
3. ✅ **Counter-based replay protection** - Prevents reuse of authentication assertions
4. ✅ **Native iOS origin validation** - Proper bundle ID checking
5. ✅ **Comprehensive security testing** - Validates against known attack vectors

**Estimated Effort:** 4-5 weeks

**Priority:** High - Current implementation has security vulnerabilities

**Next Steps:**
1. Review and approve this plan
2. Begin Phase 1 (Foundation)
3. Implement in phases with testing
4. Deploy to staging
5. Production deployment with monitoring

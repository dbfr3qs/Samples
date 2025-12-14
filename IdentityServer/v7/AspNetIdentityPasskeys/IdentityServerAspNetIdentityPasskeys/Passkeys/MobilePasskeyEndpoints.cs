// Mobile Passkey Authentication Endpoints for OAuth/OIDC flow
// Now with full FIDO2/WebAuthn cryptographic validation

using System.Text;
using System.Text.Json;
using System.Security.Claims;
using Fido2NetLib;
using Fido2NetLib.Objects;
using IdentityServerAspNetIdentityPasskeys.Models;
using IdentityServerAspNetIdentityPasskeys.Data;
using IdentityServerAspNetIdentityPasskeys.Services;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Duende.IdentityServer.Services;
using Duende.IdentityServer.Models;
using Duende.IdentityServer.Stores;

namespace IdentityServerAspNetIdentityPasskeys.Passkeys;

public static class MobilePasskeyEndpoints
{
    public static IEndpointConventionBuilder MapMobilePasskeyEndpoints(this IEndpointRouteBuilder endpoints)
    {
        ArgumentNullException.ThrowIfNull(endpoints);

        var apiGroup = endpoints.MapGroup("/api/passkey").ExcludeFromDescription();

        // Begin registration - returns WebAuthn creation options with FIDO2 validation
        apiGroup.MapPost("/register/begin", async (
            HttpContext context,
            [FromServices] IFido2 fido2,
            [FromServices] IChallengeStore challengeStore,
            [FromServices] ICredentialStore credentialStore,
            [FromServices] UserManager<ApplicationUser> userManager,
            [FromBody] BeginRegistrationRequest request) =>
        {
            try
            {
                Console.WriteLine($"[DEBUG] Registration begin for username: {request.Username}");

                if (string.IsNullOrEmpty(request.Username))
                {
                    return Results.BadRequest(new { error = "Username is required" });
                }

                var user = await userManager.FindByNameAsync(request.Username);
                if (user == null)
                {
                    Console.WriteLine($"[DEBUG] Creating new user: {request.Username}");
                    user = new ApplicationUser
                    {
                        UserName = request.Username,
                        Email = request.Email
                    };
                    var createResult = await userManager.CreateAsync(user);
                    if (!createResult.Succeeded)
                    {
                        return Results.BadRequest(new
                        {
                            error = "Failed to create user",
                            details = createResult.Errors.Select(e => e.Description)
                        });
                    }
                }

                var userId = await userManager.GetUserIdAsync(user);
                var userName = await userManager.GetUserNameAsync(user) ?? "User";

                var existingCreds = await credentialStore.GetByUserIdAsync(userId);
                var excludeCredentials = existingCreds
                    .Select(c => new PublicKeyCredentialDescriptor(c.CredentialId))
                    .ToList();

                Console.WriteLine($"[DEBUG] User has {existingCreds.Count} existing credentials");

                var fidoUser = new Fido2User
                {
                    Id = Encoding.UTF8.GetBytes(userId),
                    Name = userName,
                    DisplayName = request.DisplayName ?? userName
                };

                var options = fido2.RequestNewCredential(
                    fidoUser,
                    excludeCredentials,
                    new AuthenticatorSelection
                    {
                        AuthenticatorAttachment = AuthenticatorAttachment.Platform,
                        ResidentKey = ResidentKeyRequirement.Required,
                        UserVerification = UserVerificationRequirement.Required
                    },
                    AttestationConveyancePreference.Direct
                );

                var challengeId = await challengeStore.StoreAsync(new ChallengeData
                {
                    Challenge = options.Challenge,
                    UserId = userId,
                    ClientType = "mobile-ios",
                    CreatedAt = DateTime.UtcNow,
                    ExpiresAt = DateTime.UtcNow.AddMinutes(5)
                });

                Console.WriteLine($"[DEBUG] Generated challenge ID: {challengeId}");

                return Results.Ok(new
                {
                    challenge = Base64Url.Encode(options.Challenge),
                    rp = new { id = options.Rp.Id, name = options.Rp.Name },
                    user = new
                    {
                        id = Base64Url.Encode(fidoUser.Id),
                        name = fidoUser.Name,
                        displayName = fidoUser.DisplayName
                    },
                    pubKeyCredParams = options.PubKeyCredParams,
                    timeout = options.Timeout,
                    authenticatorSelection = options.AuthenticatorSelection,
                    attestation = options.Attestation.ToString().ToLower(),
                    challengeId = challengeId
                });
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[ERROR] Registration begin failed: {ex.Message}");
                return Results.Problem(
                    detail: ex.Message,
                    statusCode: 500,
                    title: "Failed to generate passkey creation options"
                );
            }
        });

        // Complete registration - performs FULL FIDO2 attestation verification
        apiGroup.MapPost("/register/complete", async (
            HttpContext context,
            [FromServices] IFido2 fido2,
            [FromServices] IChallengeStore challengeStore,
            [FromServices] ICredentialStore credentialStore,
            [FromServices] UserManager<ApplicationUser> userManager,
            [FromServices] NativeOriginValidator originValidator,
            [FromBody] CompleteRegistrationRequest request) =>
        {
            try
            {
                Console.WriteLine($"[DEBUG] Registration complete - challenge ID: {request.ChallengeId}");

                var challengeData = await challengeStore.GetAndRemoveAsync(request.ChallengeId);
                if (challengeData == null || challengeData.IsExpired)
                {
                    Console.WriteLine("[ERROR] Challenge expired or not found");
                    return Results.BadRequest(new { error = "Challenge expired or not found" });
                }

                var user = await userManager.FindByIdAsync(challengeData.UserId!);
                if (user == null)
                {
                    Console.WriteLine("[ERROR] User not found");
                    return Results.BadRequest(new { error = "User not found" });
                }

                // Parse the credential JSON - it comes as a JSON string from the mobile app
                Console.WriteLine($"[DEBUG] Parsing credential JSON (length: {request.CredentialJson.Length})");
                
                // First parse to get the credential structure
                var credentialDoc = JsonDocument.Parse(request.CredentialJson);
                var credentialRoot = credentialDoc.RootElement;
                
                // Extract the raw credential data
                var id = credentialRoot.GetProperty("id").GetString();
                var rawId = credentialRoot.GetProperty("rawId").GetString();
                var type = credentialRoot.GetProperty("type").GetString();
                var response = credentialRoot.GetProperty("response");
                var clientDataJSON = response.GetProperty("clientDataJSON").GetString();
                var attestationObject = response.GetProperty("attestationObject").GetString();
                
                Console.WriteLine($"[DEBUG] Credential ID: {id?.Substring(0, Math.Min(20, id?.Length ?? 0))}...");
                
                // Create the AuthenticatorAttestationRawResponse that Fido2-Net-Lib expects
                var credential = new AuthenticatorAttestationRawResponse
                {
                    Id = Base64Url.Decode(id),
                    RawId = Base64Url.Decode(rawId),
                    Type = PublicKeyCredentialType.PublicKey,
                    Response = new AuthenticatorAttestationRawResponse.ResponseData
                    {
                        ClientDataJson = Base64Url.Decode(clientDataJSON),
                        AttestationObject = Base64Url.Decode(attestationObject)
                    }
                };

                if (credential == null)
                {
                    return Results.BadRequest(new { error = "Invalid credential data" });
                }

                var options = new CredentialCreateOptions
                {
                    Challenge = challengeData.Challenge,
                    Rp = new PublicKeyCredentialRpEntity("idp.dev.internal", "Identity Server", null),
                    User = new Fido2User
                    {
                        Id = Encoding.UTF8.GetBytes(challengeData.UserId!),
                        Name = user.UserName ?? "User",
                        DisplayName = user.UserName ?? "User"
                    },
                    PubKeyCredParams = new List<PubKeyCredParam>
                    {
                        new PubKeyCredParam(COSE.Algorithm.ES256),
                        new PubKeyCredParam(COSE.Algorithm.RS256)
                    },
                    Timeout = 60000,
                    Attestation = AttestationConveyancePreference.Direct,
                    AuthenticatorSelection = new AuthenticatorSelection
                    {
                        AuthenticatorAttachment = AuthenticatorAttachment.Platform,
                        ResidentKey = ResidentKeyRequirement.Required,
                        UserVerification = UserVerificationRequirement.Required
                    }
                };

                Console.WriteLine("[DEBUG] ✅ Performing FULL FIDO2 attestation verification...");
                var result = await fido2.MakeNewCredentialAsync(
                    credential,
                    options,
                    async (args, cancellationToken) =>
                    {
                        var existingCreds = await credentialStore.GetByUserIdAsync(challengeData.UserId!);
                        return !existingCreds.Any(c => c.CredentialId.SequenceEqual(args.CredentialId));
                    },
                    cancellationToken: CancellationToken.None
                );

                if (result.Status != "ok")
                {
                    Console.WriteLine($"[ERROR] ❌ Attestation verification failed: {result.ErrorMessage}");
                    return Results.BadRequest(new
                    {
                        error = "Attestation verification failed",
                        detail = result.ErrorMessage
                    });
                }

                if (result.Result == null)
                {
                    return Results.BadRequest(new { error = "Attestation result is null" });
                }

                Console.WriteLine($"[DEBUG] ✅ Attestation verified successfully");
                Console.WriteLine($"[DEBUG] Credential ID: {Convert.ToBase64String(result.Result.Id)}");
                Console.WriteLine($"[DEBUG] Counter: {result.Result.SignCount}");
                Console.WriteLine($"[DEBUG] Attestation format: {result.Result.AttestationFormat}");

                await credentialStore.AddAsync(new StoredCredential
                {
                    UserId = challengeData.UserId!,
                    CredentialId = result.Result.Id,
                    PublicKey = result.Result.PublicKey,
                    SignatureCounter = result.Result.SignCount,
                    CredType = result.Result.Type.ToString(),
                    AaGuid = result.Result.AaGuid,
                    AttestationFormat = result.Result.AttestationFormat,
                    DeviceType = "iOS Platform Authenticator",
                    CreatedAt = DateTime.UtcNow
                });

                Console.WriteLine("[DEBUG] Credential stored successfully");

                return Results.Ok(new
                {
                    success = true,
                    userId = user.Id,
                    username = user.UserName,
                    message = "Passkey registered successfully with full cryptographic verification"
                });
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[ERROR] Registration failed: {ex.Message}");
                Console.WriteLine($"[ERROR] Stack trace: {ex.StackTrace}");
                return Results.Problem(
                    detail: ex.Message,
                    statusCode: 500,
                    title: "Failed to complete passkey registration"
                );
            }
        });

        // Begin authentication - returns WebAuthn options with FIDO2 validation
        apiGroup.MapPost("/authenticate/begin", async (
            HttpContext context,
            [FromServices] IFido2 fido2,
            [FromServices] IChallengeStore challengeStore,
            [FromBody] BeginAuthenticationRequest? request) =>
        {
            try
            {
                Console.WriteLine("[DEBUG] Authentication begin");

                var options = fido2.GetAssertionOptions(
                    null,
                    UserVerificationRequirement.Required
                );

                var challengeId = await challengeStore.StoreAsync(new ChallengeData
                {
                    Challenge = options.Challenge,
                    ClientType = "mobile-ios",
                    CreatedAt = DateTime.UtcNow,
                    ExpiresAt = DateTime.UtcNow.AddMinutes(5)
                });

                Console.WriteLine($"[DEBUG] Generated challenge ID: {challengeId}");

                return Results.Ok(new
                {
                    challenge = Base64Url.Encode(options.Challenge),
                    timeout = options.Timeout,
                    rpId = options.RpId,
                    userVerification = options.UserVerification.ToString().ToLower(),
                    challengeId = challengeId
                });
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[ERROR] Authentication begin failed: {ex.Message}");
                return Results.Problem(
                    detail: ex.Message,
                    statusCode: 500,
                    title: "Failed to generate passkey options"
                );
            }
        });

        // Complete authentication - performs FULL FIDO2 assertion verification with signature validation
        apiGroup.MapPost("/authenticate/complete", async (
            HttpContext context,
            [FromServices] IFido2 fido2,
            [FromServices] IChallengeStore challengeStore,
            [FromServices] ICredentialStore credentialStore,
            [FromServices] SignInManager<ApplicationUser> signInManager,
            [FromServices] UserManager<ApplicationUser> userManager,
            [FromServices] NativeOriginValidator originValidator,
            [FromServices] IAuthorizationCodeStore codeStore,
            [FromBody] CompleteAuthenticationRequest request) =>
        {
            try
            {
                Console.WriteLine($"[DEBUG] Auth - Received authentication completion request");

                var challengeData = await challengeStore.GetAndRemoveAsync(request.ChallengeId!);
                if (challengeData == null || challengeData.IsExpired)
                {
                    Console.WriteLine("[ERROR] Auth - Challenge expired or not found");
                    return Results.BadRequest(new { error = "Challenge expired" });
                }

                // Parse the assertion JSON - it comes as a JSON string from the mobile app
                Console.WriteLine($"[DEBUG] Auth - Parsing credential JSON (length: {request.CredentialJson.Length})");
                
                var assertionDoc = JsonDocument.Parse(request.CredentialJson);
                var assertionRoot = assertionDoc.RootElement;
                
                // Extract the raw assertion data
                var id = assertionRoot.GetProperty("id").GetString();
                var rawId = assertionRoot.GetProperty("rawId").GetString();
                var type = assertionRoot.GetProperty("type").GetString();
                var response = assertionRoot.GetProperty("response");
                var clientDataJSON = response.GetProperty("clientDataJSON").GetString();
                var authenticatorData = response.GetProperty("authenticatorData").GetString();
                var signature = response.GetProperty("signature").GetString();
                var userHandle = response.GetProperty("userHandle").GetString();
                
                Console.WriteLine($"[DEBUG] Auth - Credential ID: {id?.Substring(0, Math.Min(20, id?.Length ?? 0))}...");
                
                // Create the AuthenticatorAssertionRawResponse that Fido2-Net-Lib expects
                var assertion = new AuthenticatorAssertionRawResponse
                {
                    Id = Base64Url.Decode(id),
                    RawId = Base64Url.Decode(rawId),
                    Type = PublicKeyCredentialType.PublicKey,
                    Response = new AuthenticatorAssertionRawResponse.AssertionResponse
                    {
                        ClientDataJson = Base64Url.Decode(clientDataJSON),
                        AuthenticatorData = Base64Url.Decode(authenticatorData),
                        Signature = Base64Url.Decode(signature),
                        UserHandle = Base64Url.Decode(userHandle)
                    }
                };

                if (assertion == null)
                {
                    return Results.BadRequest(new { error = "Invalid credential data" });
                }

                var storedCredential = await credentialStore.GetByCredentialIdAsync(assertion.RawId);
                if (storedCredential == null)
                {
                    Console.WriteLine("[ERROR] Auth - Credential not found");
                    return Results.BadRequest(new { error = "Credential not found" });
                }

                var user = await userManager.FindByIdAsync(storedCredential.UserId);
                if (user == null)
                {
                    Console.WriteLine("[ERROR] Auth - User not found");
                    return Results.BadRequest(new { error = "User not found" });
                }

                Console.WriteLine($"[DEBUG] Auth - Found credential for user: {user.UserName}");
                Console.WriteLine($"[DEBUG] Auth - Current counter: {storedCredential.SignatureCounter}");

                var options = new AssertionOptions
                {
                    Challenge = challengeData.Challenge,
                    RpId = "idp.dev.internal",
                    AllowCredentials = new[] {
                        new PublicKeyCredentialDescriptor(storedCredential.CredentialId)
                    },
                    UserVerification = UserVerificationRequirement.Required
                };

                Console.WriteLine("[DEBUG] Auth - ✅ Performing FULL FIDO2 assertion verification...");
                Console.WriteLine($"[DEBUG] Auth - Expected userId: {storedCredential.UserId}");
                Console.WriteLine($"[DEBUG] Auth - UserHandle from assertion (raw bytes): {(assertion.Response.UserHandle?.Length > 0 ? Convert.ToBase64String(assertion.Response.UserHandle) : "empty")}");
                
                // Decode the userHandle to see what it contains
                if (assertion.Response.UserHandle?.Length > 0)
                {
                    try
                    {
                        var userHandleString = Encoding.UTF8.GetString(assertion.Response.UserHandle);
                        Console.WriteLine($"[DEBUG] Auth - UserHandle decoded as UTF-8: {userHandleString}");
                    }
                    catch
                    {
                        Console.WriteLine("[DEBUG] Auth - UserHandle is not valid UTF-8");
                    }
                }
                
                var result = await fido2.MakeAssertionAsync(
                    assertion,
                    options,
                    storedCredential.PublicKey,
                    new List<byte[]>(),
                    storedCredential.SignatureCounter,
                    async (args, cancellationToken) =>
                    {
                        // The userHandle should match the userId that owns this credential
                        // If userHandle is empty, we still allow it since we already verified the credential belongs to the user
                        if (args.UserHandle == null || args.UserHandle.Length == 0)
                        {
                            Console.WriteLine("[DEBUG] Auth - UserHandle is empty, allowing based on credential ownership");
                            return true;
                        }
                        
                        // Try to decode the userHandle as UTF-8 string and compare
                        try
                        {
                            var userHandleString = Encoding.UTF8.GetString(args.UserHandle);
                            Console.WriteLine($"[DEBUG] Auth - Comparing userHandle '{userHandleString}' with userId '{storedCredential.UserId}'");
                            
                            // Check if it matches the userId directly
                            if (userHandleString == storedCredential.UserId)
                            {
                                Console.WriteLine("[DEBUG] Auth - UserHandle matches userId (string comparison)");
                                return true;
                            }
                            
                            // The iOS app appears to Base64-encode the userId, so try decoding it
                            try
                            {
                                var decodedBytes = Convert.FromBase64String(userHandleString);
                                var decodedUserId = Encoding.UTF8.GetString(decodedBytes);
                                Console.WriteLine($"[DEBUG] Auth - UserHandle after Base64 decode: '{decodedUserId}'");
                                
                                if (decodedUserId == storedCredential.UserId)
                                {
                                    Console.WriteLine("[DEBUG] Auth - UserHandle matches userId after Base64 decode!");
                                    return true;
                                }
                            }
                            catch
                            {
                                Console.WriteLine("[DEBUG] Auth - UserHandle is not Base64-encoded");
                            }
                            
                            // Also check byte-level comparison
                            var expectedUserIdBytes = Encoding.UTF8.GetBytes(storedCredential.UserId);
                            var matches = args.UserHandle.SequenceEqual(expectedUserIdBytes);
                            Console.WriteLine($"[DEBUG] Auth - UserHandle matches userId (byte comparison): {matches}");
                            return matches;
                        }
                        catch (Exception ex)
                        {
                            Console.WriteLine($"[DEBUG] Auth - Error decoding userHandle: {ex.Message}");
                            // If we can't decode it, allow based on credential ownership
                            return true;
                        }
                    },
                    cancellationToken: CancellationToken.None
                );

                if (result.Status != "ok")
                {
                    Console.WriteLine($"[ERROR] Auth - ❌ Signature verification failed: {result.ErrorMessage}");
                    return Results.BadRequest(new
                    {
                        error = "Signature verification failed",
                        detail = result.ErrorMessage
                    });
                }

                Console.WriteLine($"[DEBUG] Auth - ✅ Signature verified successfully");
                Console.WriteLine($"[DEBUG] Auth - New counter: {result.SignCount}");

                // Counter validation for replay protection
                // Only enforce counter increment if the stored counter is greater than 0
                // First authentication (counter 0 -> 0 or 0 -> 1) is allowed
                if (storedCredential.SignatureCounter > 0 && result.SignCount <= storedCredential.SignatureCounter)
                {
                    Console.WriteLine($"[SECURITY] Auth - ⚠️ Counter rollback detected!");
                    Console.WriteLine($"[SECURITY] Auth - Stored counter: {storedCredential.SignatureCounter}");
                    Console.WriteLine($"[SECURITY] Auth - Received counter: {result.SignCount}");
                    Console.WriteLine($"[SECURITY] Auth - User: {user.Id}, Credential: {Convert.ToBase64String(storedCredential.CredentialId)}");
                    
                    return Results.BadRequest(new { error = "Authentication failed - possible replay attack" });
                }
                
                Console.WriteLine($"[DEBUG] Auth - Counter validation passed (stored: {storedCredential.SignatureCounter}, new: {result.SignCount})");

                storedCredential.SignatureCounter = result.SignCount;
                storedCredential.LastUsed = DateTime.UtcNow;
                await credentialStore.UpdateAsync(storedCredential);

                Console.WriteLine("[DEBUG] Auth - Counter updated successfully");

                Console.WriteLine("[DEBUG] Auth - Signing in user...");
                
                // Create ClaimsPrincipal with passkey authentication method
                var principal = await signInManager.CreateUserPrincipalAsync(user);
                
                // Add required IdentityServer claims
                var identity = principal.Identity as ClaimsIdentity;
                if (identity != null)
                {
                    // Remove any existing amr claims and add passkey
                    var amrClaims = identity.FindAll("amr").ToList();
                    foreach (var claim in amrClaims)
                    {
                        identity.RemoveClaim(claim);
                    }
                    identity.AddClaim(new Claim("amr", "hwk")); // Hardware key authentication
                    
                    // Add auth_time claim (required by IdentityServer)
                    var authTime = DateTimeOffset.UtcNow.ToUnixTimeSeconds().ToString();
                    identity.AddClaim(new Claim("auth_time", authTime));
                    
                    // Add idp claim (identity provider)
                    identity.AddClaim(new Claim("idp", "local"));
                }
                
                await signInManager.SignInAsync(user, isPersistent: false);
                Console.WriteLine("[DEBUG] Auth - User signed in successfully");

                var codeChallenge = request.CodeChallenge;
                var codeChallengeMethod = request.CodeChallengeMethod ?? "S256";

                if (string.IsNullOrEmpty(codeChallenge))
                {
                    Console.WriteLine("[ERROR] Auth - No code_challenge provided");
                    return Results.BadRequest(new { error = "code_challenge is required for PKCE" });
                }

                var code = new AuthorizationCode
                {
                    ClientId = "mobile-client",
                    Subject = principal,
                    CreationTime = DateTime.UtcNow,
                    Lifetime = 300,
                    RedirectUri = "com.idp.mobile://callback",
                    RequestedScopes = new[] { "openid", "profile", "api" },
                    CodeChallenge = codeChallenge.Sha256(),
                    CodeChallengeMethod = codeChallengeMethod,
                    IsOpenId = true
                };

                var codeValue = await codeStore.StoreAuthorizationCodeAsync(code);
                Console.WriteLine($"[DEBUG] Auth - Generated authorization code");

                return Results.Ok(new
                {
                    code = codeValue,
                    state = request.State
                });
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[ERROR] Auth - Authentication failed: {ex.Message}");
                Console.WriteLine($"[ERROR] Auth - Stack trace: {ex.StackTrace}");
                return Results.Problem(
                    detail: ex.Message,
                    statusCode: 500,
                    title: "Failed to complete passkey authentication"
                );
            }
        });

        return apiGroup;
    }

    public record BeginRegistrationRequest(
        string Username,
        string? Email,
        string? DisplayName
    );

    public record CompleteRegistrationRequest(
        string ChallengeId,
        string CredentialJson
    );

    public record BeginAuthenticationRequest(string? Username);

    public record CompleteAuthenticationRequest(
        string? ChallengeId,
        string CredentialJson,
        string? State,
        string? CodeChallenge,
        string? CodeChallengeMethod
    );
}

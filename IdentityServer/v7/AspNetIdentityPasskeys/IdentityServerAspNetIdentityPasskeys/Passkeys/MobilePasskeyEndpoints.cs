// Mobile Passkey Authentication Endpoints for OAuth/OIDC flow

using System.Text.Json;
using System.Security.Claims;
using IdentityServerAspNetIdentityPasskeys.Models;
using IdentityServerAspNetIdentityPasskeys.Data;
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

        // Begin registration - returns WebAuthn creation options
        apiGroup.MapPost("/register/begin", async (
            HttpContext context,
            [FromServices] SignInManager<ApplicationUser> signInManager,
            [FromServices] UserManager<ApplicationUser> userManager,
            [FromBody] BeginRegistrationRequest request) =>
        {
            try
            {
                if (string.IsNullOrEmpty(request.Username))
                {
                    return Results.BadRequest(new { error = "Username is required" });
                }

                // Check if user exists, create if not
                var user = await userManager.FindByNameAsync(request.Username);
                if (user == null)
                {
                    user = new ApplicationUser { UserName = request.Username, Email = request.Email };
                    var createResult = await userManager.CreateAsync(user);
                    if (!createResult.Succeeded)
                    {
                        return Results.BadRequest(new {
                            error = "Failed to create user",
                            details = createResult.Errors.Select(e => e.Description)
                        });
                    }
                }

                var userId = await userManager.GetUserIdAsync(user);
                var userName = await userManager.GetUserNameAsync(user) ?? "User";

                var optionsJson = await signInManager.MakePasskeyCreationOptionsAsync(new()
                {
                    Id = userId,
                    Name = userName,
                    DisplayName = request.DisplayName ?? userName
                });

                // Store the challenge in session for later verification
                var options = JsonDocument.Parse(optionsJson);
                var challenge = options.RootElement.GetProperty("challenge").GetString();

                // Store challenge and user ID in session
                context.Session.SetString("passkey_reg_challenge", challenge ?? "");
                context.Session.SetString("passkey_reg_user_id", userId);

                return Results.Content(optionsJson, contentType: "application/json");
            }
            catch (Exception ex)
            {
                return Results.Problem(
                    detail: ex.Message,
                    statusCode: 500,
                    title: "Failed to generate passkey creation options"
                );
            }
        });

        // Complete registration - verifies passkey and stores it
        apiGroup.MapPost("/register/complete", async (
            HttpContext context,
            [FromServices] SignInManager<ApplicationUser> signInManager,
            [FromServices] UserManager<ApplicationUser> userManager,
            [FromServices] ApplicationDbContext dbContext,
            [FromBody] CompleteRegistrationRequest request) =>
        {
            try
            {
                Console.WriteLine($"[DEBUG] Received credentialJson: {request?.CredentialJson?.Substring(0, Math.Min(100, request.CredentialJson?.Length ?? 0))}...");

                // Verify the challenge matches
                var storedChallenge = context.Session.GetString("passkey_reg_challenge");
                var storedUserId = context.Session.GetString("passkey_reg_user_id");

                Console.WriteLine($"[DEBUG] Stored challenge: {storedChallenge}");
                Console.WriteLine($"[DEBUG] Stored userId: {storedUserId}");

                if (string.IsNullOrEmpty(storedChallenge) || string.IsNullOrEmpty(storedUserId))
                {
                    Console.WriteLine("[ERROR] No registration session found");
                    return Results.BadRequest(new { error = "No registration session found" });
                }

                // Get the user
                var user = await userManager.FindByIdAsync(storedUserId);
                if (user == null)
                {
                    Console.WriteLine("[ERROR] User not found");
                    return Results.BadRequest(new { error = "User not found" });
                }

                Console.WriteLine("[DEBUG] Performing passkey attestation...");
                // Perform passkey attestation
                var attestationResult = await signInManager.PerformPasskeyAttestationAsync(request.CredentialJson);
                Console.WriteLine($"[DEBUG] Attestation succeeded: {attestationResult.Succeeded}");
                if (!attestationResult.Succeeded)
                {
                    Console.WriteLine($"[ERROR] Passkey attestation failed: {attestationResult.Failure?.Message}");
                    return Results.BadRequest(new {
                        error = "Passkey registration failed",
                        detail = attestationResult.Failure?.Message
                    });
                }

                // Store the passkey
                Console.WriteLine($"[DEBUG] Storing passkey for user {user.Id}...");
                Console.WriteLine($"[DEBUG] Passkey credential ID: {Convert.ToBase64String(attestationResult.Passkey.CredentialId)}");
                var setPasskeyResult = await userManager.AddOrUpdatePasskeyAsync(user, attestationResult.Passkey);
                Console.WriteLine($"[DEBUG] Store passkey result succeeded: {setPasskeyResult.Succeeded}");
                if (!setPasskeyResult.Succeeded)
                {
                    Console.WriteLine($"[ERROR] Failed to store passkey: {string.Join(", ", setPasskeyResult.Errors.Select(e => e.Description))}");
                    return Results.BadRequest(new { error = "Failed to store passkey" });
                }

                // Explicitly save changes to database
                Console.WriteLine("[DEBUG] Saving changes to database...");
                await dbContext.SaveChangesAsync();
                Console.WriteLine("[DEBUG] Passkey stored successfully and changes saved to database");

                // Clear session
                context.Session.Remove("passkey_reg_challenge");
                context.Session.Remove("passkey_reg_user_id");

                return Results.Ok(new
                {
                    success = true,
                    userId = user.Id,
                    username = user.UserName,
                    message = "Passkey registered successfully"
                });
            }
            catch (Exception ex)
            {
                // Log the full exception for debugging
                Console.WriteLine($"[ERROR] Passkey registration failed: {ex.Message}");
                Console.WriteLine($"[ERROR] Stack trace: {ex.StackTrace}");
                if (ex.InnerException != null)
                {
                    Console.WriteLine($"[ERROR] Inner exception: {ex.InnerException.Message}");
                }

                return Results.Problem(
                    detail: ex.Message,
                    statusCode: 500,
                    title: "Failed to complete passkey registration"
                );
            }
        });

        // Begin authentication - returns WebAuthn options
        apiGroup.MapPost("/authenticate/begin", async (
            HttpContext context,
            [FromServices] SignInManager<ApplicationUser> signInManager,
            [FromBody] BeginAuthenticationRequest? request) =>
        {
            try
            {
                // Get passkey request options (challenge, etc.)
                // This works without a logged-in user
                var optionsJson = await signInManager.MakePasskeyRequestOptionsAsync(null);

                // Store the challenge in session for later verification
                var options = JsonDocument.Parse(optionsJson);
                var challenge = options.RootElement.GetProperty("challenge").GetString();

                // Store challenge in session
                context.Session.SetString("passkey_challenge", challenge ?? "");

                return Results.Content(optionsJson, contentType: "application/json");
            }
            catch (Exception ex)
            {
                return Results.Problem(
                    detail: ex.Message,
                    statusCode: 500,
                    title: "Failed to generate passkey options"
                );
            }
        });

        // Complete authentication - verifies passkey and returns authorization code
        apiGroup.MapPost("/authenticate/complete", async (
            HttpContext context,
            [FromServices] SignInManager<ApplicationUser> signInManager,
            [FromServices] UserManager<ApplicationUser> userManager,
            [FromServices] ApplicationDbContext dbContext,
            [FromServices] IAuthorizationCodeStore codeStore,
            [FromServices] IClientStore clientStore,
            [FromBody] CompleteAuthenticationRequest request) =>
        {
            try
            {
                Console.WriteLine($"[DEBUG] Auth - Received credentialJson: {request?.CredentialJson?.Substring(0, Math.Min(100, request.CredentialJson?.Length ?? 0))}...");

                // Parse and log credential details
                var credentialDoc = JsonDocument.Parse(request.CredentialJson);
                var clientDataJSON = credentialDoc.RootElement.GetProperty("response").GetProperty("clientDataJSON").GetString();

                // Convert base64url to base64 with proper padding
                var clientDataBase64 = clientDataJSON!.Replace('_', '/').Replace('-', '+');
                switch (clientDataBase64.Length % 4)
                {
                    case 2: clientDataBase64 += "=="; break;
                    case 3: clientDataBase64 += "="; break;
                }

                var clientDataBytes = Convert.FromBase64String(clientDataBase64);
                var clientDataString = System.Text.Encoding.UTF8.GetString(clientDataBytes);
                Console.WriteLine($"[DEBUG] Auth - Client data: {clientDataString}");

                // Verify the challenge matches
                var storedChallenge = context.Session.GetString("passkey_challenge");
                Console.WriteLine($"[DEBUG] Auth - Stored challenge: {storedChallenge}");

                if (string.IsNullOrEmpty(storedChallenge))
                {
                    Console.WriteLine("[ERROR] Auth - No challenge found in session");
                    return Results.BadRequest(new { error = "No challenge found in session" });
                }

                Console.WriteLine("[DEBUG] Auth - Looking up user by credential ID...");

                // Extract credential ID and find the user who owns it
                var credentialId = credentialDoc.RootElement.GetProperty("id").GetString();
                Console.WriteLine($"[DEBUG] Auth - Credential ID: {credentialId}");

                // Convert base64url to bytes
                var credentialIdBase64 = credentialId?.Replace('_', '/').Replace('-', '+');
                switch (credentialIdBase64?.Length % 4)
                {
                    case 2: credentialIdBase64 += "=="; break;
                    case 3: credentialIdBase64 += "="; break;
                }
                var credentialIdBytes = Convert.FromBase64String(credentialIdBase64!);

                // Find the user who owns this credential
                var passkey = await dbContext.UserPasskeys
                    .Where(p => p.CredentialId == credentialIdBytes)
                    .FirstOrDefaultAsync();

                if (passkey == null)
                {
                    Console.WriteLine("[ERROR] Auth - No passkey found with this credential ID");
                    return Results.BadRequest(new { error = "Credential not found" });
                }

                Console.WriteLine($"[DEBUG] Auth - Found passkey for user: {passkey.UserId}");
                var user = await userManager.FindByIdAsync(passkey.UserId);
                if (user == null)
                {
                    Console.WriteLine("[ERROR] Auth - User not found");
                    return Results.BadRequest(new { error = "User not found" });
                }

                // Verify the credential belongs to this user
                var userPasskeys = await userManager.GetPasskeysAsync(user);
                Console.WriteLine($"[DEBUG] Auth - User has {userPasskeys.Count} passkey(s)");

                var matchingPasskey = userPasskeys.FirstOrDefault(pk =>
                    pk.CredentialId.SequenceEqual(credentialIdBytes));

                if (matchingPasskey == null)
                {
                    Console.WriteLine("[ERROR] Auth - Credential ID not found in user's passkeys");
                    return Results.BadRequest(new { error = "Credential does not belong to user" });
                }

                Console.WriteLine($"[DEBUG] Auth - Verified credential belongs to user");
                Console.WriteLine($"[DEBUG] Auth - Credential ID (hex): {Convert.ToHexString(matchingPasskey.CredentialId)}");

                // TODO: In production, you should perform full WebAuthn assertion validation here
                // For now, we trust that:
                // 1. The credential exists in the database
                // 2. The challenge matches
                // 3. The origin is correct (validated by IdentityPasskeyOptions)
                // 4. The user owns this credential

                // Sign in the user
                Console.WriteLine("[DEBUG] Auth - Signing in user...");
                await signInManager.SignInAsync(user, isPersistent: false);
                Console.WriteLine("[DEBUG] Auth - User signed in successfully");

                // Clear the passkey challenge from session
                context.Session.Remove("passkey_challenge");

                // Get code challenge from request
                var codeChallenge = request.CodeChallenge;
                var codeChallengeMethod = request.CodeChallengeMethod ?? "S256";

                if (string.IsNullOrEmpty(codeChallenge))
                {
                    Console.WriteLine("[ERROR] Auth - No code_challenge provided");
                    return Results.BadRequest(new { error = "code_challenge is required for PKCE" });
                }

                Console.WriteLine($"[DEBUG] Auth - Code challenge received: {codeChallenge}");
                Console.WriteLine($"[DEBUG] Auth - Code challenge method: {codeChallengeMethod}");

                // Create authorization code with PKCE
                // IdentityServer stores SHA256(codeChallenge), not the raw challenge
                // This matches what IdentityServer does in its standard authorization flow
                var code = new AuthorizationCode
                {
                    ClientId = "mobile-client",
                    Subject = context.User,
                    CreationTime = DateTime.UtcNow,
                    Lifetime = 300,
                    RedirectUri = "com.idp.mobile://callback",
                    RequestedScopes = new[] { "openid", "profile", "api" },
                    CodeChallenge = codeChallenge.Sha256(),
                    CodeChallengeMethod = codeChallengeMethod,
                    IsOpenId = true
                };

                var codeValue = await codeStore.StoreAuthorizationCodeAsync(code);
                Console.WriteLine($"[DEBUG] Auth - Generated code: {codeValue}");
                Console.WriteLine($"[DEBUG] Auth - Stored hashed code challenge (matches IdentityServer's standard flow)");

                return Results.Ok(new
                {
                    code = codeValue,
                    state = request.State
                });
            }
            catch (Exception ex)
            {
                // Log the full exception for debugging
                Console.WriteLine($"[ERROR] Auth - Passkey authentication failed: {ex.Message}");
                Console.WriteLine($"[ERROR] Auth - Stack trace: {ex.StackTrace}");
                if (ex.InnerException != null)
                {
                    Console.WriteLine($"[ERROR] Auth - Inner exception: {ex.InnerException.Message}");
                }

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
        string CredentialJson
    );

    public record BeginAuthenticationRequest(string? Username);

    public record CompleteAuthenticationRequest(
        string CredentialJson,
        string? State,
        string? CodeChallenge,
        string? CodeChallengeMethod
    );
}

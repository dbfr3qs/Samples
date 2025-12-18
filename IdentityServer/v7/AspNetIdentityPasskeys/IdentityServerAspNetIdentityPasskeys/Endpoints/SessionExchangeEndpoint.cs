using IdentityServerAspNetIdentityPasskeys.Models;
using IdentityServerAspNetIdentityPasskeys.Services;
using IdentityServerAspNetIdentityPasskeys.Data;
using Microsoft.AspNetCore.Mvc;

namespace IdentityServerAspNetIdentityPasskeys.Endpoints;

public static class SessionExchangeEndpoint
{
    public static IEndpointRouteBuilder MapSessionExchange(this IEndpointRouteBuilder endpoints)
    {
        endpoints.MapPost("/connect/session-exchange", HandleSessionExchange)
            .WithName("SessionExchange");

        return endpoints;
    }

    private static async Task<IResult> HandleSessionExchange(
        [FromBody] SessionExchangeRequest request,
        SessionExchangeValidator validator,
        HttpContext httpContext,
        ILogger<SessionExchangeRequest> logger,
        [FromServices] Microsoft.AspNetCore.Identity.SignInManager<ApplicationUser> signInManager,
        [FromServices] Microsoft.AspNetCore.DataProtection.IDataProtectionProvider dataProtectionProvider)
    {
        logger.LogInformation("🔐 [SessionExchange] Received session exchange request");

        if (string.IsNullOrEmpty(request.Assertion))
        {
            logger.LogWarning("❌ [SessionExchange] Missing assertion");
            return Results.BadRequest(new { error = "invalid_request", error_description = "Missing assertion" });
        }

        var httpMethod = httpContext.Request.Method;
        var httpUri = $"{httpContext.Request.Scheme}://{httpContext.Request.Host}{httpContext.Request.Path}";

        var validationResult = await validator.ValidateAsync(request.Assertion, httpMethod, httpUri);

        if (!validationResult.IsValid)
        {
            logger.LogWarning("❌ [SessionExchange] Validation failed: {Error}", validationResult.ErrorMessage);
            return Results.Unauthorized();
        }

        var mobileSession = validationResult.Session!;

        logger.LogInformation("✅ [SessionExchange] Validation successful for session {SessionId}", mobileSession.SessionId);

        // Get the user to create authentication cookie
        var user = await signInManager.UserManager.FindByIdAsync(mobileSession.SubjectId);
        if (user == null)
        {
            logger.LogWarning("❌ [SessionExchange] User not found: {SubjectId}", mobileSession.SubjectId);
            return Results.Unauthorized();
        }

        // Create claims principal for the user
        var claimsPrincipalFactory = httpContext.RequestServices.GetRequiredService<Microsoft.AspNetCore.Identity.IUserClaimsPrincipalFactory<ApplicationUser>>();
        var principal = await claimsPrincipalFactory.CreateAsync(user);
        
        // Add required IdentityServer claims
        var identity = principal.Identity as System.Security.Claims.ClaimsIdentity;
        if (identity != null)
        {
            // Add idp claim (identity provider) - required by IdentityServer
            identity.AddClaim(new System.Security.Claims.Claim("idp", "local"));
            
            // Add amr claim (authentication method reference)
            identity.AddClaim(new System.Security.Claims.Claim("amr", "pwd"));
            
            // Add auth_time claim (authentication time)
            var authTime = DateTimeOffset.UtcNow.ToUnixTimeSeconds().ToString();
            identity.AddClaim(new System.Security.Claims.Claim("auth_time", authTime));
        }
        
        logger.LogInformation("📋 [SessionExchange] Created principal with {ClaimCount} claims for user {UserId}", 
            principal.Claims.Count(), user.Id);
        
        // Create authentication properties
        var authProps = new Microsoft.AspNetCore.Authentication.AuthenticationProperties
        {
            IsPersistent = false,
            ExpiresUtc = DateTimeOffset.UtcNow.AddHours(10),
            IssuedUtc = DateTimeOffset.UtcNow
        };

        // Create authentication ticket
        var ticket = new Microsoft.AspNetCore.Authentication.AuthenticationTicket(
            principal,
            authProps,
            "Identity.Application"
        );

        // Create data protector with the same purpose string as ASP.NET Core Identity cookies
        // The purpose string must match exactly what CookieAuthenticationHandler uses
        var protector = dataProtectionProvider
            .CreateProtector("Microsoft.AspNetCore.Authentication.Cookies.CookieAuthenticationMiddleware")
            .CreateProtector("Identity.Application")
            .CreateProtector("v2");
        
        // Serialize and protect the ticket
        var ticketSerializer = new Microsoft.AspNetCore.Authentication.TicketSerializer();
        var ticketBytes = ticketSerializer.Serialize(ticket);
        var protectedBytes = protector.Protect(ticketBytes);
        
        // Encode to Base64Url (ASP.NET Core cookie format)
        var actualCookieValue = Microsoft.AspNetCore.WebUtilities.Base64UrlTextEncoder.Encode(protectedBytes);
        
        logger.LogInformation("📦 [SessionExchange] Created cookie value (length: {Length}, first 50 chars: {Preview})", 
            actualCookieValue.Length, 
            actualCookieValue.Length > 50 ? actualCookieValue.Substring(0, 50) : actualCookieValue);

        // Create session cookies that can be injected into the WebView
        var cookies = new List<CookieData>
        {
            new CookieData
            {
                Name = ".AspNetCore.Identity.Application",
                Value = actualCookieValue,
                Domain = "idp.dev.internal", // Must match the IdP domain exactly
                Path = "/",
                Secure = true,
                HttpOnly = true,
                SameSite = "None",
                Expires = new DateTimeOffset(mobileSession.Expires).ToUnixTimeSeconds()
            }
        };
        
        logger.LogInformation("📦 [SessionExchange] Created authentication cookie for user {UserId}", user.Id);

        var response = new SessionExchangeResponse
        {
            SessionId = mobileSession.SessionId,
            Cookies = cookies,
            ExpiresAt = new DateTimeOffset(mobileSession.Expires).ToUnixTimeSeconds()
        };

        return Results.Ok(response);
    }
}

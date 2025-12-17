using IdentityServerAspNetIdentityPasskeys.Models;
using IdentityServerAspNetIdentityPasskeys.Services;
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
        ILogger<SessionExchangeRequest> logger)
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
        var sessionKey = mobileSession.Key;

        logger.LogInformation("✅ [SessionExchange] Validation successful for session {SessionId}", mobileSession.SessionId);

        // Create session cookies that can be injected into the WebView
        var cookies = new List<CookieData>();

        // Create the IdentityServer session cookie using the session key
        // This is what IdentityServer uses to look up the session
        cookies.Add(new CookieData
        {
            Name = "idsrv.session",
            Value = sessionKey, // Use the session Key, not SessionId
            Domain = ".dev.internal", // Use wildcard domain for idp.dev.internal
            Path = "/",
            Secure = true,
            HttpOnly = false,
            SameSite = "None",
            Expires = new DateTimeOffset(mobileSession.Expires).ToUnixTimeSeconds()
        });

        logger.LogInformation("📦 [SessionExchange] Returning session cookie with key: {SessionKey}", sessionKey);

        var response = new SessionExchangeResponse
        {
            SessionId = mobileSession.SessionId,
            Cookies = cookies,
            ExpiresAt = new DateTimeOffset(mobileSession.Expires).ToUnixTimeSeconds()
        };

        return Results.Ok(response);
    }
}

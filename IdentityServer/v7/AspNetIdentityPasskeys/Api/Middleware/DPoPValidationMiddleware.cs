using Api.Services;
using Microsoft.IdentityModel.JsonWebTokens;
using System.Text.Json;

namespace Api.Middleware;

public class DPoPValidationMiddleware
{
    private readonly RequestDelegate _next;
    private readonly ILogger<DPoPValidationMiddleware> _logger;

    public DPoPValidationMiddleware(RequestDelegate next, ILogger<DPoPValidationMiddleware> logger)
    {
        _next = next;
        _logger = logger;
    }

    public async Task InvokeAsync(HttpContext context, DPoPProofValidator dpopValidator)
    {
        // Only validate DPoP for authenticated requests
        if (context.User?.Identity?.IsAuthenticated != true)
        {
            await _next(context);
            return;
        }

        var dpopHeader = context.Request.Headers["DPoP"].FirstOrDefault();
        if (string.IsNullOrEmpty(dpopHeader))
        {
            _logger.LogWarning("❌ [DPoP] No DPoP header provided for authenticated request");
            context.Response.StatusCode = 401;
            context.Response.Headers["WWW-Authenticate"] = "DPoP error=\"invalid_token\", error_description=\"DPoP proof required\"";
            await context.Response.WriteAsJsonAsync(new { error = "invalid_token", error_description = "DPoP proof required" });
            return;
        }

        var authHeader = context.Request.Headers["Authorization"].FirstOrDefault();
        var accessToken = authHeader?.Replace("Bearer ", "");

        var httpMethod = context.Request.Method;
        var httpUri = $"{context.Request.Scheme}://{context.Request.Host}{context.Request.Path}";

        _logger.LogInformation("🔐 [DPoP] Validating DPoP proof for {Method} {Uri}", httpMethod, httpUri);

        var validationResult = await dpopValidator.ValidateAsync(dpopHeader, httpMethod, httpUri, accessToken);
        if (!validationResult.IsValid)
        {
            _logger.LogWarning("❌ [DPoP] Validation failed: {Error}", validationResult.ErrorMessage);
            context.Response.StatusCode = 401;
            context.Response.Headers["WWW-Authenticate"] = $"DPoP error=\"invalid_dpop_proof\", error_description=\"{validationResult.ErrorMessage}\"";
            await context.Response.WriteAsJsonAsync(new { error = "invalid_dpop_proof", error_description = validationResult.ErrorMessage });
            return;
        }

        // Verify the access token is bound to this DPoP key
        var cnfClaim = context.User.FindFirst("cnf")?.Value;
        if (!string.IsNullOrEmpty(cnfClaim))
        {
            try
            {
                var cnf = JsonSerializer.Deserialize<Dictionary<string, string>>(cnfClaim);
                var jkt = cnf?.GetValueOrDefault("jkt");

                if (jkt != validationResult.Thumbprint)
                {
                    _logger.LogWarning("❌ [DPoP] Token binding mismatch: token jkt={TokenJkt}, proof jkt={ProofJkt}", jkt, validationResult.Thumbprint);
                    context.Response.StatusCode = 401;
                    context.Response.Headers["WWW-Authenticate"] = "DPoP error=\"invalid_dpop_proof\", error_description=\"Token not bound to this key\"";
                    await context.Response.WriteAsJsonAsync(new { error = "invalid_dpop_proof", error_description = "Token not bound to this key" });
                    return;
                }

                _logger.LogInformation("✅ [DPoP] Token binding verified: jkt={Jkt}", jkt);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "❌ [DPoP] Failed to parse cnf claim");
                context.Response.StatusCode = 401;
                context.Response.Headers["WWW-Authenticate"] = "DPoP error=\"invalid_token\", error_description=\"Invalid cnf claim\"";
                await context.Response.WriteAsJsonAsync(new { error = "invalid_token", error_description = "Invalid cnf claim" });
                return;
            }
        }

        _logger.LogInformation("✅ [DPoP] Validation successful, thumbprint: {Thumbprint}", validationResult.Thumbprint);

        await _next(context);
    }
}

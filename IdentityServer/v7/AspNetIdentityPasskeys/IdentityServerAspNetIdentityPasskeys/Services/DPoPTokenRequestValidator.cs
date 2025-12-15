using Duende.IdentityServer.Validation;
using System.Security.Claims;
using Microsoft.IdentityModel.Tokens;
using System.Text.Json;

namespace IdentityServerAspNetIdentityPasskeys.Services;

public class DPoPTokenRequestValidator : ICustomTokenRequestValidator
{
    private readonly DPoPProofValidator _dpopValidator;
    private readonly IDeviceBindingStore _deviceBindingStore;
    private readonly IHttpContextAccessor _httpContextAccessor;
    private readonly ILogger<DPoPTokenRequestValidator> _logger;

    public DPoPTokenRequestValidator(
        DPoPProofValidator dpopValidator,
        IDeviceBindingStore deviceBindingStore,
        IHttpContextAccessor httpContextAccessor,
        ILogger<DPoPTokenRequestValidator> logger)
    {
        _dpopValidator = dpopValidator;
        _deviceBindingStore = deviceBindingStore;
        _httpContextAccessor = httpContextAccessor;
        _logger = logger;
    }

    public async Task ValidateAsync(CustomTokenRequestValidationContext context)
    {
        var httpContext = _httpContextAccessor.HttpContext;
        if (httpContext == null)
        {
            return;
        }

        var dpopHeader = httpContext.Request.Headers["DPoP"].FirstOrDefault();
        if (string.IsNullOrEmpty(dpopHeader))
        {
            _logger.LogDebug("No DPoP header present in token request");
            return;
        }

        _logger.LogInformation("DPoP header detected, validating proof...");

        var httpMethod = httpContext.Request.Method;
        var httpUri = $"{httpContext.Request.Scheme}://{httpContext.Request.Host}{httpContext.Request.Path}";

        var validationResult = await _dpopValidator.ValidateAsync(dpopHeader, httpMethod, httpUri);
        if (!validationResult.IsValid)
        {
            _logger.LogWarning("DPoP validation failed: {Error}", validationResult.ErrorMessage);
            context.Result.IsError = true;
            context.Result.Error = "invalid_dpop_proof";
            context.Result.ErrorDescription = validationResult.ErrorMessage;
            return;
        }

        _logger.LogInformation("DPoP proof validated successfully, thumbprint: {Thumbprint}", validationResult.Thumbprint);

        if (context.Result.ValidatedRequest.GrantType == "authorization_code")
        {
            await HandleAuthorizationCodeGrantAsync(context, validationResult);
        }
        else if (context.Result.ValidatedRequest.GrantType == "refresh_token")
        {
            await HandleRefreshTokenGrantAsync(context, validationResult);
        }
    }

    private async Task HandleAuthorizationCodeGrantAsync(CustomTokenRequestValidationContext context, DPoPValidationResult validationResult)
    {
        _logger.LogInformation("Creating device binding for authorization code grant");

        var subject = context.Result.ValidatedRequest.Subject;
        if (subject == null)
        {
            _logger.LogWarning("No subject found in validated request");
            return;
        }

        var userId = subject.FindFirst("sub")?.Value;
        if (string.IsNullOrEmpty(userId))
        {
            _logger.LogWarning("No user ID found in subject");
            return;
        }

        var jwkJson = JsonSerializer.Serialize(validationResult.Jwk, new JsonSerializerOptions 
        { 
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase 
        });

        // Check if binding already exists for this thumbprint
        var existingBinding = await _deviceBindingStore.GetByThumbprintAsync(validationResult.Thumbprint!);
        
        if (existingBinding != null)
        {
            _logger.LogInformation("Device binding already exists for thumbprint {Thumbprint}, updating last used time", validationResult.Thumbprint);
            existingBinding.LastUsedAt = DateTime.UtcNow;
            await _deviceBindingStore.UpdateAsync(existingBinding);
        }
        else
        {
            _logger.LogInformation("Creating new device binding for user {UserId} with thumbprint {Thumbprint}", userId, validationResult.Thumbprint);
            var binding = new Models.DeviceBinding
            {
                UserId = userId,
                CredentialId = Array.Empty<byte>(),
                DPoPPublicKeyJwk = jwkJson,
                PublicKeyThumbprint = validationResult.Thumbprint!,
                CreatedAt = DateTime.UtcNow,
                LastUsedAt = DateTime.UtcNow,
                RefreshCount = 0
            };

            await _deviceBindingStore.CreateAsync(binding);
        }

        context.Result.ValidatedRequest.ClientClaims.Add(new Claim("dpop_jkt", validationResult.Thumbprint!));
        context.Result.CustomResponse = new Dictionary<string, object>
        {
            { "token_type", "DPoP" }
        };

        _logger.LogInformation("Device binding processed for user {UserId} with thumbprint {Thumbprint}", userId, validationResult.Thumbprint);
    }

    private async Task HandleRefreshTokenGrantAsync(CustomTokenRequestValidationContext context, DPoPValidationResult validationResult)
    {
        _logger.LogInformation("Validating device binding for refresh token grant");

        var refreshTokenHandle = context.Result.ValidatedRequest.RefreshTokenHandle;
        if (string.IsNullOrEmpty(refreshTokenHandle))
        {
            _logger.LogWarning("No refresh token handle found");
            context.Result.IsError = true;
            context.Result.Error = "invalid_grant";
            context.Result.ErrorDescription = "Invalid refresh token";
            return;
        }

        var binding = await _deviceBindingStore.GetByThumbprintAsync(validationResult.Thumbprint!);
        if (binding == null)
        {
            _logger.LogWarning("No device binding found for thumbprint {Thumbprint}", validationResult.Thumbprint);
            context.Result.IsError = true;
            context.Result.Error = "invalid_dpop_proof";
            context.Result.ErrorDescription = "Device binding not found";
            return;
        }

        binding.LastUsedAt = DateTime.UtcNow;
        binding.RefreshCount++;
        binding.RefreshTokenHandle = refreshTokenHandle;
        await _deviceBindingStore.UpdateAsync(binding);

        context.Result.ValidatedRequest.ClientClaims.Add(new Claim("dpop_jkt", validationResult.Thumbprint!));
        context.Result.CustomResponse = new Dictionary<string, object>
        {
            { "token_type", "DPoP" }
        };

        _logger.LogInformation("Device binding validated and updated for thumbprint {Thumbprint}, refresh count: {Count}", 
            validationResult.Thumbprint, binding.RefreshCount);
    }
}

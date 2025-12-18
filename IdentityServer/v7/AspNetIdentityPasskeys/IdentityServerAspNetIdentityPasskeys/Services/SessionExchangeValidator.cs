using Duende.IdentityServer.Models;
using Duende.IdentityServer.Services;
using Duende.IdentityServer.Stores;
using Microsoft.IdentityModel.JsonWebTokens;
using Microsoft.IdentityModel.Tokens;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using JsonWebKey = Microsoft.IdentityModel.Tokens.JsonWebKey;

namespace IdentityServerAspNetIdentityPasskeys.Services;

public class SessionExchangeValidator
{
    private readonly IReplayCache _replayCache;
    private readonly IMobileSessionStore _mobileSessionStore;
    private readonly IDeviceBindingStore _deviceBindingStore;
    private readonly ILogger<SessionExchangeValidator> _logger;

    public SessionExchangeValidator(
        IReplayCache replayCache,
        IMobileSessionStore mobileSessionStore,
        IDeviceBindingStore deviceBindingStore,
        ILogger<SessionExchangeValidator> logger)
    {
        _replayCache = replayCache;
        _mobileSessionStore = mobileSessionStore;
        _deviceBindingStore = deviceBindingStore;
        _logger = logger;
    }

    public async Task<SessionExchangeValidationResult> ValidateAsync(string assertion, string httpMethod, string httpUri)
    {
        try
        {
            var handler = new JsonWebTokenHandler();
            var token = handler.ReadJsonWebToken(assertion);

            if (token.GetHeaderValue<string>("typ") != "dpop+jwt")
            {
                return SessionExchangeValidationResult.Failure("Invalid typ header");
            }

            var alg = token.GetHeaderValue<string>("alg");
            if (alg != "ES256")
            {
                return SessionExchangeValidationResult.Failure("Invalid alg header, only ES256 is supported");
            }

            var jwkJson = token.GetHeaderValue<string>("jwk");
            if (string.IsNullOrEmpty(jwkJson))
            {
                return SessionExchangeValidationResult.Failure("Missing jwk in header");
            }

            var jwk = JsonSerializer.Deserialize<JsonWebKey>(jwkJson);
            if (jwk == null || jwk.Kty != "EC" || jwk.Crv != "P-256")
            {
                return SessionExchangeValidationResult.Failure("Invalid JWK, must be EC P-256");
            }

            var validationParameters = new TokenValidationParameters
            {
                ValidateIssuer = false,
                ValidateAudience = false,
                ValidateLifetime = false,
                IssuerSigningKey = jwk
            };

            var result = await handler.ValidateTokenAsync(assertion, validationParameters);
            if (!result.IsValid)
            {
                return SessionExchangeValidationResult.Failure($"Signature validation failed: {result.Exception?.Message}");
            }

            var jti = token.GetClaim("jti")?.Value;
            if (string.IsNullOrEmpty(jti))
            {
                return SessionExchangeValidationResult.Failure("Missing jti claim");
            }

            var htm = token.GetClaim("htm")?.Value;
            if (htm != httpMethod)
            {
                return SessionExchangeValidationResult.Failure($"htm mismatch: expected {httpMethod}, got {htm}");
            }

            var htu = token.GetClaim("htu")?.Value;
            if (!ValidateHtu(htu, httpUri))
            {
                return SessionExchangeValidationResult.Failure($"htu mismatch: expected {httpUri}, got {htu}");
            }

            var iat = token.GetClaim("iat")?.Value;
            if (string.IsNullOrEmpty(iat) || !long.TryParse(iat, out var iatValue))
            {
                return SessionExchangeValidationResult.Failure("Missing or invalid iat claim");
            }

            var now = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
            if (Math.Abs(now - iatValue) > 60)
            {
                return SessionExchangeValidationResult.Failure("Proof timestamp outside acceptable window (60 seconds)");
            }

            if (await _replayCache.ExistsAsync(jti))
            {
                return SessionExchangeValidationResult.Failure("Proof has already been used (replay attack)");
            }

            var sid = token.GetClaim("sid")?.Value;
            var sub = token.GetClaim("sub")?.Value;
            
            if (string.IsNullOrEmpty(sid))
            {
                return SessionExchangeValidationResult.Failure("Missing sid claim");
            }
            
            if (string.IsNullOrEmpty(sub))
            {
                return SessionExchangeValidationResult.Failure("Missing sub claim");
            }

            _logger.LogInformation("🔍 [SessionExchange] Looking up mobile session with ID: {SessionId}", sid);
            _logger.LogInformation("🔍 [SessionExchange] User subject: {SubjectId}", sub);
            
            var mobileSession = await _mobileSessionStore.GetSessionByIdAsync(sid);
            if (mobileSession == null)
            {
                _logger.LogWarning("⚠️ [SessionExchange] Mobile session not found for ID: {SessionId}", sid);
                return SessionExchangeValidationResult.Failure("Session not found");
            }
            
            _logger.LogInformation("✅ [SessionExchange] Found mobile session: {SessionId}, SubjectId: {SubjectId}", mobileSession.SessionId, mobileSession.SubjectId);

            if (mobileSession.SubjectId != sub)
            {
                return SessionExchangeValidationResult.Failure("Session does not belong to user");
            }

            var thumbprint = ComputeThumbprint(jwk);
            
            // Verify device binding: DPoP key must match the one used during authentication
            if (!string.IsNullOrEmpty(mobileSession.DPoPKeyThumbprint))
            {
                if (mobileSession.DPoPKeyThumbprint != thumbprint)
                {
                    _logger.LogWarning("❌ [SessionExchange] Device binding violation! Expected thumbprint: {Expected}, Got: {Actual}",
                        mobileSession.DPoPKeyThumbprint, thumbprint);
                    return SessionExchangeValidationResult.Failure("DPoP key does not match device binding");
                }
                
                _logger.LogInformation("✅ [SessionExchange] Device binding verified - DPoP key matches");
            }
            else
            {
                _logger.LogWarning("⚠️ [SessionExchange] No device binding stored for session {SessionId}", sid);
            }

            await _replayCache.AddAsync(jti, TimeSpan.FromMinutes(5));

            _logger.LogInformation("✅ [SessionExchange] Validation successful for session {SessionId}, user {SubjectId}", sid, sub);

            return SessionExchangeValidationResult.Success(mobileSession, sub, thumbprint);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "❌ [SessionExchange] Validation error");
            return SessionExchangeValidationResult.Failure($"Validation error: {ex.Message}");
        }
    }

    private bool ValidateHtu(string? htu, string expectedUri)
    {
        if (string.IsNullOrEmpty(htu))
            return false;

        if (!Uri.TryCreate(htu, UriKind.Absolute, out var htuUri) ||
            !Uri.TryCreate(expectedUri, UriKind.Absolute, out var expectedUriObj))
            return false;

        return htuUri.Scheme == expectedUriObj.Scheme &&
               htuUri.Host == expectedUriObj.Host &&
               htuUri.Port == expectedUriObj.Port &&
               htuUri.AbsolutePath == expectedUriObj.AbsolutePath;
    }

    private string ComputeThumbprint(JsonWebKey jwk)
    {
        var thumbprintJson = JsonSerializer.Serialize(new
        {
            crv = jwk.Crv,
            kty = jwk.Kty,
            x = jwk.X,
            y = jwk.Y
        }, new JsonSerializerOptions { PropertyNamingPolicy = JsonNamingPolicy.CamelCase });

        using var sha256 = SHA256.Create();
        var hash = sha256.ComputeHash(Encoding.UTF8.GetBytes(thumbprintJson));
        return Base64UrlEncoder.Encode(hash);
    }
}

public class SessionExchangeValidationResult
{
    public bool IsValid { get; set; }
    public string? ErrorMessage { get; set; }
    public MobileSession? Session { get; set; }
    public string? SubjectId { get; set; }
    public string? Thumbprint { get; set; }

    public static SessionExchangeValidationResult Success(MobileSession session, string subjectId, string thumbprint)
    {
        return new SessionExchangeValidationResult
        {
            IsValid = true,
            Session = session,
            SubjectId = subjectId,
            Thumbprint = thumbprint
        };
    }

    public static SessionExchangeValidationResult Failure(string errorMessage)
    {
        return new SessionExchangeValidationResult
        {
            IsValid = false,
            ErrorMessage = errorMessage
        };
    }
}

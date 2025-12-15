using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Microsoft.IdentityModel.Tokens;
using Microsoft.IdentityModel.JsonWebTokens;

namespace IdentityServerAspNetIdentityPasskeys.Services;

public class DPoPProofValidator
{
    private readonly IReplayCache _replayCache;

    public DPoPProofValidator(IReplayCache replayCache)
    {
        _replayCache = replayCache;
    }

    public async Task<DPoPValidationResult> ValidateAsync(string dpopProof, string httpMethod, string httpUri, string? accessToken = null)
    {
        try
        {
            var handler = new JsonWebTokenHandler();
            var token = handler.ReadJsonWebToken(dpopProof);

            if (token.GetHeaderValue<string>("typ") != "dpop+jwt")
            {
                return DPoPValidationResult.Failure("Invalid typ header");
            }

            var alg = token.GetHeaderValue<string>("alg");
            if (alg != "ES256")
            {
                return DPoPValidationResult.Failure("Invalid alg header, only ES256 is supported");
            }

            var jwkJson = token.GetHeaderValue<string>("jwk");
            if (string.IsNullOrEmpty(jwkJson))
            {
                return DPoPValidationResult.Failure("Missing jwk in header");
            }

            var jwk = JsonSerializer.Deserialize<JsonWebKey>(jwkJson);
            if (jwk == null || jwk.Kty != "EC" || jwk.Crv != "P-256")
            {
                return DPoPValidationResult.Failure("Invalid JWK, must be EC P-256");
            }

            var validationParameters = new TokenValidationParameters
            {
                ValidateIssuer = false,
                ValidateAudience = false,
                ValidateLifetime = false,
                IssuerSigningKey = jwk
            };

            var result = await handler.ValidateTokenAsync(dpopProof, validationParameters);
            if (!result.IsValid)
            {
                return DPoPValidationResult.Failure($"Signature validation failed: {result.Exception?.Message}");
            }

            var jti = token.GetClaim("jti")?.Value;
            if (string.IsNullOrEmpty(jti))
            {
                return DPoPValidationResult.Failure("Missing jti claim");
            }

            var htm = token.GetClaim("htm")?.Value;
            if (htm != httpMethod)
            {
                return DPoPValidationResult.Failure($"htm mismatch: expected {httpMethod}, got {htm}");
            }

            var htu = token.GetClaim("htu")?.Value;
            if (!ValidateHtu(htu, httpUri))
            {
                return DPoPValidationResult.Failure($"htu mismatch: expected {httpUri}, got {htu}");
            }

            var iat = token.GetClaim("iat")?.Value;
            if (string.IsNullOrEmpty(iat) || !long.TryParse(iat, out var iatValue))
            {
                return DPoPValidationResult.Failure("Missing or invalid iat claim");
            }

            var now = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
            if (Math.Abs(now - iatValue) > 60)
            {
                return DPoPValidationResult.Failure("Proof timestamp outside acceptable window (60 seconds)");
            }

            if (await _replayCache.ExistsAsync(jti))
            {
                return DPoPValidationResult.Failure("Proof has already been used (replay attack)");
            }

            await _replayCache.AddAsync(jti, TimeSpan.FromMinutes(5));

            if (accessToken != null)
            {
                var ath = token.GetClaim("ath")?.Value;
                var expectedAth = ComputeAth(accessToken);
                if (ath != expectedAth)
                {
                    return DPoPValidationResult.Failure("ath claim does not match access token");
                }
            }

            var thumbprint = ComputeThumbprint(jwk);

            return DPoPValidationResult.Success(thumbprint, jwk);
        }
        catch (Exception ex)
        {
            return DPoPValidationResult.Failure($"Validation error: {ex.Message}");
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

    private string ComputeAth(string accessToken)
    {
        using var sha256 = SHA256.Create();
        var hash = sha256.ComputeHash(Encoding.ASCII.GetBytes(accessToken));
        return Base64UrlEncoder.Encode(hash);
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

public class DPoPValidationResult
{
    public bool IsValid { get; set; }
    public string? ErrorMessage { get; set; }
    public string? Thumbprint { get; set; }
    public JsonWebKey? Jwk { get; set; }

    public static DPoPValidationResult Success(string thumbprint, JsonWebKey jwk)
    {
        return new DPoPValidationResult
        {
            IsValid = true,
            Thumbprint = thumbprint,
            Jwk = jwk
        };
    }

    public static DPoPValidationResult Failure(string errorMessage)
    {
        return new DPoPValidationResult
        {
            IsValid = false,
            ErrorMessage = errorMessage
        };
    }
}

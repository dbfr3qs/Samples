// Copyright (c) Duende Software. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Security.Cryptography;
using Microsoft.IdentityModel.Tokens;

namespace WebClient;

/// <summary>
/// Helper class to generate DPoP proofs for demonstration purposes
/// </summary>
public static class DPoPProofGenerator
{
    private static RsaSecurityKey? _rsaKey;
    private static JsonWebKey? _jwk;

    /// <summary>
    /// Initialize the DPoP proof generator with a shared key
    /// </summary>
    public static void Initialize(RsaSecurityKey rsaKey, JsonWebKey jwk)
    {
        _rsaKey = rsaKey;
        _jwk = jwk;
    }

    /// <summary>
    /// Creates a DPoP proof token for the authorize endpoint
    /// </summary>
    public static string CreateProofForAuthorize(string? redirectUri, HttpRequest request)
    {
        if (_rsaKey == null || _jwk == null)
        {
            throw new InvalidOperationException("DPoPProofGenerator not initialized. Call Initialize() first.");
        }

        // Build the authorize URL
        var authorizeUrl = $"{request.Scheme}://{request.Host}/connect/authorize";

        // Create the DPoP proof JWT
        var handler = new JwtSecurityTokenHandler();
        
        var descriptor = new SecurityTokenDescriptor
        {
            Claims = new Dictionary<string, object>
            {
                { "htm", "GET" },
                { "htu", authorizeUrl },
                { "jti", Guid.NewGuid().ToString() },
                { "iat", DateTimeOffset.UtcNow.ToUnixTimeSeconds() }
            },
            SigningCredentials = new SigningCredentials(
                _rsaKey, 
                SecurityAlgorithms.RsaSsaPssSha256)
        };

        // Add the JWK to the token header
        var token = handler.CreateJwtSecurityToken(descriptor);
        token.Header["typ"] = "dpop+jwt";
        
        // Convert JsonWebKey to dictionary for JWT header
        token.Header["jwk"] = new Dictionary<string, string>
        {
            { "kty", _jwk.Kty },
            { "e", _jwk.E },
            { "n", _jwk.N },
            { "alg", _jwk.Alg }
        };

        return handler.WriteToken(token);
    }

    /// <summary>
    /// Gets the JWK thumbprint for the current key
    /// </summary>
    public static string GetJwkThumbprint()
    {
        if (_jwk == null)
        {
            throw new InvalidOperationException("JWK not initialized. Call CreateProofForAuthorize first.");
        }

        return Base64UrlEncoder.Encode(_jwk.ComputeJwkThumbprint());
    }
}

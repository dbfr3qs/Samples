// Copyright (c) Duende Software. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.


using Duende.IdentityServer.Models;

namespace IdentityServerHost;

public static class Clients
{
    public static IEnumerable<Client> List =>
        new[]
        {
            new Client
            {
                ClientId = "dpop",
                // "905e4892-7610-44cb-a122-6209b38c882f" hashed
                ClientSecrets = { new Secret("H+90jjtmDc3/HiNmtKwuBZG9eNOvpahx2jscGscejqE=") },

                AllowedGrantTypes = GrantTypes.CodeAndClientCredentials,

                RedirectUris = { "https://localhost:5010/signin-oidc" },
                FrontChannelLogoutUri = "https://localhost:5010/signout-oidc",
                PostLogoutRedirectUris = { "https://localhost:5010/signout-callback-oidc" },

                AllowOfflineAccess = true,
                AllowedScopes = { "openid", "profile", "scope1" },

                RequireDPoP = true,
                
                // Enable PAR to demonstrate that it doesn't prevent the attack
                // when request_uri is exposed and reusable in the front-channel
                RequirePushedAuthorization = true,
            },
            
            // Public client for attack demo (no client secret required)
            new Client
            {
                ClientId = "dpop_public",
                RequireClientSecret = false, // Public client
                
                AllowedGrantTypes = GrantTypes.Code,
                
                RedirectUris = { "https://localhost:5010/signin-oidc" },
                FrontChannelLogoutUri = "https://localhost:5010/signout-oidc",
                PostLogoutRedirectUris = { "https://localhost:5010/signout-callback-oidc" },
                
                AllowOfflineAccess = true,
                AllowedScopes = { "openid", "profile", "scope1" },
                
                RequireDPoP = true,
                RequirePkce = true, // Public clients should use PKCE
                
                // Disable PAR for attack demo so dpop_jkt is visible in URL
                RequirePushedAuthorization = false,
            },
        };
}

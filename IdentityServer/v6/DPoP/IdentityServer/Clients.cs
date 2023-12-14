﻿// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.


using Duende.IdentityServer.Models;
using System.Collections.Generic;
using IdentityModel;

namespace IdentityServerHost
{
    public static class Clients
    {
        public static IEnumerable<Client> List =>
            new []
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
                    RequireDPoP = true
                },
                new Client
                {
                    ClientId = "js_oidc",
                    AllowedGrantTypes = GrantTypes.Code,
                    
                    RedirectUris =
                    {
                        "http://localhost:8080/",
                        "http://localhost:8080/index.html",
                        "http://localhost:8080/callback.html",
                        "http://localhost:8080/silent.html",
                        "http://localhost:8080/modal.html",
                        "http://localhost:1234/"
                    },
                    AllowedCorsOrigins =
                    {
                        "http://localhost:1234"
                    },
                    AllowOfflineAccess = true,
                    AllowedScopes = { "openid", "profile", "email" },
                    RequireClientSecret = false,
                    RequirePkce = true,
                    RequireDPoP = true,
                    DPoPValidationMode = DPoPTokenExpirationValidationMode.Nonce,
                    AlwaysIncludeUserClaimsInIdToken = true
                }
            };
    }
}
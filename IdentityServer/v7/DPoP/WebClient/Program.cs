// Copyright (c) Duende Software. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

using System.Security.Cryptography;
using System.Text.Json;
using Duende.AccessTokenManagement.DPoP;
using Duende.AccessTokenManagement.OpenIdConnect;
using Microsoft.AspNetCore.Authentication;
using Microsoft.IdentityModel.Tokens;
using Serilog;
using WebClient;

Console.Title = "WebClient";

Log.Logger = new LoggerConfiguration()
    .MinimumLevel.Information()
    .Enrich.FromLogContext()
    .WriteTo.Console(outputTemplate: "[{Timestamp:HH:mm:ss} {Level}] {SourceContext}{NewLine}{Message:lj}{NewLine}{Exception}{NewLine}")
    .CreateLogger();

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddSerilog();
builder.Services.AddControllersWithViews();

// Create a shared DPoP key for both authorize and token endpoints
var sharedRsaKey = new RsaSecurityKey(RSA.Create(2048));
var sharedJwk = JsonWebKeyConverter.ConvertFromSecurityKey(sharedRsaKey);
sharedJwk.Alg = "PS256";

// Initialize the DPoP proof generator with the shared key
DPoPProofGenerator.Initialize(sharedRsaKey, sharedJwk);

// add cookie-based session management with OpenID Connect authentication
builder.Services.AddAuthentication(options =>
{
    options.DefaultScheme = "cookie";
    options.DefaultChallengeScheme = "oidc";
})
    .AddCookie("cookie", options =>
    {
        options.Cookie.Name = "dpop";

        options.ExpireTimeSpan = TimeSpan.FromHours(8);
        options.SlidingExpiration = false;

        options.Events.OnSigningOut = async e =>
        {
            // automatically revoke refresh token at signout time
            await e.HttpContext.RevokeRefreshTokenAsync();
        };
    })
    .AddOpenIdConnect("oidc", options =>
    {
        options.Authority = "https://localhost:5001";
        options.RequireHttpsMetadata = false;

        options.ClientId = "dpop";
        options.ClientSecret = "905e4892-7610-44cb-a122-6209b38c882f";

        // code flow + PKCE (PKCE is turned on by default)
        options.ResponseType = "code";
        options.ResponseMode = "query";
        options.UsePkce = true;

        // Enable PAR to demonstrate that it doesn't prevent the attack
        // when request_uri is exposed in the front-channel
        options.PushedAuthorizationBehavior =
            Microsoft.AspNetCore.Authentication.OpenIdConnect.PushedAuthorizationBehavior.Require;

        options.Scope.Clear();
        options.Scope.Add("openid");
        options.Scope.Add("profile");
        options.Scope.Add("scope1");
        options.Scope.Add("offline_access");

        // keeps id_token smaller
        options.GetClaimsFromUserInfoEndpoint = true;
        options.SaveTokens = true;
        options.MapInboundClaims = false;
        options.DisableTelemetry = true;

        options.Events.OnRedirectToIdentityProvider = context =>
        {
            var logger = context.HttpContext.RequestServices.GetRequiredService<ILogger<Program>>();
            
            var authUrl = context.ProtocolMessage.CreateAuthenticationRequestUrl();
            logger.LogInformation("=== AUTHORIZATION REQUEST ===");
            logger.LogInformation("Full URL: {Url}", authUrl);
            logger.LogInformation("Response Mode: {ResponseMode}", context.ProtocolMessage.ResponseMode);
            logger.LogInformation("Response Type: {ResponseType}", context.ProtocolMessage.ResponseType);
            
            // Log request_uri if using PAR
            if (!string.IsNullOrEmpty(context.ProtocolMessage.RequestUri))
            {
                logger.LogWarning("⚠️ PAR request_uri exposed in front-channel: {RequestUri}", context.ProtocolMessage.RequestUri);
                logger.LogWarning("⚠️ This request_uri can be stolen and reused by an attacker!");
            }
            
            return Task.CompletedTask;
        };

        options.TokenValidationParameters = new TokenValidationParameters
        {
            NameClaimType = "name",
            RoleClaimType = "role"
        };
    });

// this is only needed if you want to override/prevent dpop from being used on certain API endpoints
//services.AddTransient<IDPoPProofService, CustomProofService>();

// add automatic token management
builder.Services.AddOpenIdConnectAccessTokenManagement(options =>
{
    // Use the same DPoP JWK that we're using for the authorize endpoint
    var rsaKey = new RsaSecurityKey(RSA.Create(2048));
    var jwkKey = JsonWebKeyConverter.ConvertFromSecurityKey(rsaKey);
    jwkKey.Alg = "PS256";
    var jwk = JsonSerializer.Serialize(jwkKey);
    options.DPoPJsonWebKey = DPoPProofKey.ParseOrDefault(jwk);
});

// add HTTP client to call protected API
builder.Services.AddUserAccessTokenHttpClient("client", configureClient: client =>
{
    client.BaseAddress = new Uri("https://localhost:5005");
});

var app = builder.Build();

app.UseDeveloperExceptionPage();
app.UseHttpsRedirection();
app.UseStaticFiles();

app.UseRouting();

app.UseAuthentication();
app.UseAuthorization();

app.MapDefaultControllerRoute().RequireAuthorization();

app.Run();

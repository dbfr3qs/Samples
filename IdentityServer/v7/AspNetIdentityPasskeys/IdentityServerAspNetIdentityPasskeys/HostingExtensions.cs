// Copyright (c) Duende Software. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

using System.Globalization;
using Duende.IdentityServer;
using Fido2NetLib;
using IdentityServerAspNetIdentityPasskeys.Data;
using IdentityServerAspNetIdentityPasskeys.Models;
using IdentityServerAspNetIdentityPasskeys.Passkeys;
using IdentityServerAspNetIdentityPasskeys.Services;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using Serilog;
using Serilog.Filters;

namespace IdentityServerAspNetIdentityPasskeys;

internal static class HostingExtensions
{
    public static WebApplicationBuilder ConfigureLogging(this WebApplicationBuilder builder)
    {
        // Write most logs to the console but diagnostic data to a file.
        // See https://docs.duendesoftware.com/identityserver/diagnostics/data
        builder.Host.UseSerilog((ctx, lc) =>
        {
            lc.WriteTo.Logger(consoleLogger =>
            {
                consoleLogger.WriteTo.Console(
                    outputTemplate:
                    "[{Timestamp:HH:mm:ss} {Level}] {SourceContext}{NewLine}{Message:lj}{NewLine}{Exception}{NewLine}",
                    formatProvider: CultureInfo.InvariantCulture);
                if (builder.Environment.IsDevelopment())
                {
                    consoleLogger.Filter.ByExcluding(Matching.FromSource("Duende.IdentityServer.Diagnostics.Summary"));
                }
            });
            if (builder.Environment.IsDevelopment())
            {
                lc.WriteTo.Logger(fileLogger =>
                {
                    fileLogger
                        .WriteTo.File("./diagnostics/diagnostic.log", rollingInterval: RollingInterval.Day,
                            fileSizeLimitBytes: 1024 * 1024 * 10, // 10 MB
                            rollOnFileSizeLimit: true,
                            outputTemplate:
                            "[{Timestamp:HH:mm:ss} {Level}] {SourceContext}{NewLine}{Message:lj}{NewLine}{Exception}{NewLine}",
                            formatProvider: CultureInfo.InvariantCulture)
                        .Filter
                        .ByIncludingOnly(Matching.FromSource("Duende.IdentityServer.Diagnostics.Summary"));
                }).Enrich.FromLogContext().ReadFrom.Configuration(ctx.Configuration);
            }
        });
        return builder;
    }

    public static WebApplication ConfigureServices(this WebApplicationBuilder builder)
    {
        builder.Services.AddRazorPages();
        
        // Add session support for mobile passkey authentication
        builder.Services.AddDistributedMemoryCache();
        builder.Services.AddSession(options =>
        {
            options.IdleTimeout = TimeSpan.FromMinutes(10);
            options.Cookie.HttpOnly = true;
            options.Cookie.IsEssential = true;
        });

        builder.Services.AddDbContext<ApplicationDbContext>(options =>
            options.UseSqlite(builder.Configuration.GetConnectionString("DefaultConnection")));

        builder.Services.AddIdentity<ApplicationUser, IdentityRole>(options =>
            {
                options.Stores.SchemaVersion = IdentitySchemaVersions.Version3;
            })
            .AddEntityFrameworkStores<ApplicationDbContext>()
            .AddDefaultTokenProviders();

        if (builder.Environment.IsDevelopment())
        {
            builder.Services.Configure<IdentityPasskeyOptions>(options =>
            {
                // Allow localhost and idp.dev.internal origins for development.
                options.ValidateOrigin = context => ValueTask.FromResult(
                    context.Origin == "https://localhost:5001" || 
                    context.Origin == "https://localhost" ||
                    context.Origin == "https://idp.dev.internal:5001" ||
                    context.Origin == "https://idp.dev.internal");
            });
        }

        // Configure Fido2 for native passkey support
        builder.Services.AddSingleton<IFido2>(sp =>
        {
            return new Fido2(new Fido2Configuration
            {
                ServerDomain = "idp.dev.internal",
                ServerName = "Identity Server",
                Origins = new HashSet<string>
                {
                    "https://idp.dev.internal",
                    "https://idp.dev.internal:5001",
                    "https://localhost:5001",
                    "https://localhost",
                    "ios:bundle-id://com.idp.mobile",
                    "ios:bundle-id://com.idp.mobiledemo"
                },
                TimestampDriftTolerance = 60000
            });
        });

        // Register passkey services
        builder.Services.AddScoped<IChallengeStore, ChallengeStore>();
        builder.Services.AddScoped<ICredentialStore, CredentialStore>();
        builder.Services.AddSingleton<NativeOriginValidator>();
        builder.Services.AddHostedService<ChallengeCleanupService>();

        // Register DPoP services
        builder.Services.AddScoped<IPrfOutputStore, PrfOutputStore>();
        builder.Services.AddScoped<DPoPProofValidator>();
        builder.Services.AddScoped<IReplayCache, ReplayCache>();
        builder.Services.AddScoped<IDeviceBindingStore, DeviceBindingStore>();
        builder.Services.AddHttpContextAccessor();

        builder.Services
            .AddIdentityServer(options =>
            {
                options.Events.RaiseErrorEvents = true;
                options.Events.RaiseInformationEvents = true;
                options.Events.RaiseFailureEvents = true;
                options.Events.RaiseSuccessEvents = true;

                // Use a large chunk size for diagnostic data in development where it will be redirected to a local file.
                if (builder.Environment.IsDevelopment())
                {
                    options.Diagnostics.ChunkSize = 1024 * 1024 * 10; // 10 MB
                }
            })
            .AddInMemoryIdentityResources(Config.IdentityResources)
            .AddInMemoryApiScopes(Config.ApiScopes)
            .AddInMemoryApiResources(Config.ApiResources)
            .AddInMemoryClients(Config.Clients)
            .AddAspNetIdentity<ApplicationUser>()
            .AddServerSideSessions()
            .AddLicenseSummary();

        // Register DPoP custom validators
        builder.Services.AddTransient<Duende.IdentityServer.Validation.ICustomTokenRequestValidator, DPoPTokenRequestValidator>();
        builder.Services.AddTransient<Duende.IdentityServer.Services.ISessionCoordinationService, DPoPSessionCoordinator>();

        builder.Services.AddAuthentication()
            .AddOpenIdConnect("oidc", "Sign-in with demo.duendesoftware.com", options =>
            {
                options.SignInScheme = IdentityServerConstants.ExternalCookieAuthenticationScheme;
                options.SignOutScheme = IdentityServerConstants.SignoutScheme;
                options.SaveTokens = true;

                options.Authority = "https://demo.duendesoftware.com";
                options.ClientId = "interactive.confidential";
                options.ClientSecret = "secret";
                options.ResponseType = "code";

                options.TokenValidationParameters = new TokenValidationParameters
                {
                    NameClaimType = "name",
                    RoleClaimType = "role"
                };
            });

        return builder.Build();
    }

    public static WebApplication ConfigurePipeline(this WebApplication app)
    {
        app.UseSerilogRequestLogging();

        if (app.Environment.IsDevelopment())
        {
            app.UseDeveloperExceptionPage();
        }

        app.UseStaticFiles();
        
        // Serve Apple App Site Association file with correct content type
        app.UseStaticFiles(new StaticFileOptions
        {
            ServeUnknownFileTypes = true,
            DefaultContentType = "application/json",
            OnPrepareResponse = ctx =>
            {
                if (ctx.File.Name == "apple-app-site-association")
                {
                    ctx.Context.Response.Headers.Append("Content-Type", "application/json");
                }
            }
        });
        
        app.UseRouting();
        
        // Enable session for passkey challenge storage
        app.UseSession();
        
        app.UseIdentityServer();
        app.UseAuthorization();

        app.MapPasskeyEndpoints();
        app.MapMobilePasskeyEndpoints(); // Mobile passkey endpoints with full FIDO2/WebAuthn cryptographic validation

        app.MapRazorPages()
            .RequireAuthorization();

        return app;
    }
}

// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.


using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Hosting;
using Microsoft.EntityFrameworkCore;
using Duende.IdentityServer;
using System;
using Microsoft.IdentityModel.Protocols.OpenIdConnect;
using Microsoft.IdentityModel.Tokens;

namespace IdentityServerHost
{
    public class Startup
    {
        public IWebHostEnvironment Environment { get; }
        public IConfiguration Configuration { get; }

        public Startup(IWebHostEnvironment environment, IConfiguration configuration)
        {
            Environment = environment;
            Configuration = configuration;
        }

        public void ConfigureServices(IServiceCollection services)
        {
            services.AddRazorPages();

            var connectionString = Configuration.GetConnectionString("DefaultConnection");

            var builder = services.AddIdentityServer(options =>
            {
                options.Events.RaiseErrorEvents = true;
                options.Events.RaiseInformationEvents = true;
                options.Events.RaiseFailureEvents = true;
                options.Events.RaiseSuccessEvents = true;

                // see https://docs.duendesoftware.com/identityserver/v5/fundamentals/resources/
                options.EmitStaticAudienceClaim = true;

                // this controls how long the dynamic providers are cached, if caching is enabled (see AddConfigurationStoreCache() below)
                options.Caching.IdentityProviderCacheDuration = TimeSpan.FromMinutes(15);
            })
                .AddTestUsers(TestUsers.Users)
                // this adds the config data from DB (clients, resources, CORS)
                .AddConfigurationStore(options =>
                {
                    options.ConfigureDbContext = b =>
                        b.UseSqlite(connectionString, dbOpts => dbOpts.MigrationsAssembly(typeof(Startup).Assembly.FullName));
                })
                // this adds the operational data from DB (codes, tokens, consents)
                .AddOperationalStore(options =>
                {
                    options.ConfigureDbContext = b =>
                        b.UseSqlite(connectionString, dbOpts => dbOpts.MigrationsAssembly(typeof(Startup).Assembly.FullName));

                    // this enables automatic token cleanup. this is optional.
                    options.EnableTokenCleanup = true;
                })
                // this enables caching for data loaded from the configuration store (including dynamic providers)
                .AddConfigurationStoreCache();


            services.AddAuthentication("xero")
                .AddCookie("xero", cookieOptions =>
                {
                    cookieOptions.Cookie.Name = "xero";
                    cookieOptions.SlidingExpiration = false;
                    cookieOptions.ExpireTimeSpan = TimeSpan.FromMinutes(60);
                    cookieOptions.Cookie.IsEssential = true;
                    cookieOptions.Cookie.SameSite = Microsoft.AspNetCore.Http.SameSiteMode.None;
                });
                
            services.AddAuthentication()
                .AddOpenIdConnect("Cozone", "Cozone mock",
                    oidcOptions =>
                        {
                            oidcOptions.SignInScheme = IdentityServerConstants.ExternalCookieAuthenticationScheme;
                            oidcOptions.SignOutScheme = "xero";
                            oidcOptions.ClientId = "xero";
                            oidcOptions.ClientSecret = "YkMtj9hb75W9EfNK";
                            oidcOptions.ResponseType = OpenIdConnectResponseType.Code;
                            oidcOptions.Scope.Remove("profile");
                            oidcOptions.Authority = "https://localhost:5311/";
                            oidcOptions.RequireHttpsMetadata = true;
                            oidcOptions.CallbackPath = "/signin-cozone";
                            oidcOptions.RemoteSignOutPath = "/signout-cozone"; //do not remove
                            oidcOptions.SignedOutCallbackPath = "/signout-callback-cozone"; //do not remove
                        })
                .AddOpenIdConnect("demoidsrv", "IdentityServer", options =>
                {
                    options.SignInScheme = IdentityServerConstants.ExternalCookieAuthenticationScheme;
                    options.SignOutScheme = IdentityServerConstants.SignoutScheme;

                    options.Authority = "https://demo.duendesoftware.com";
                    options.ClientId = "login";
                    options.ResponseType = "id_token";
                    options.SaveTokens = true;
                    options.CallbackPath = "/signin-idsrv";
                    options.SignedOutCallbackPath = "/signout-callback-idsrv";
                    options.RemoteSignOutPath = "/signout-idsrv";
                    options.MapInboundClaims = false;

                    options.TokenValidationParameters = new TokenValidationParameters
                    {
                        NameClaimType = "name",
                        RoleClaimType = "role"
                    };
                });
        }

        public void Configure(IApplicationBuilder app)
        {
            if (Environment.IsDevelopment())
            {
                app.UseDeveloperExceptionPage();
            }

            app.UseStaticFiles();

            app.UseRouting();
            app.UseIdentityServer();
            app.UseAuthorization();
            app.UseEndpoints(endpoints =>
            {
                endpoints.MapRazorPages();
            });
        }
    }
}

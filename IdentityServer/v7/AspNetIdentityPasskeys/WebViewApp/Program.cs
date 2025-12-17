var builder = WebApplication.CreateBuilder(args);

// Configure Kestrel to use the development certificate
builder.WebHost.ConfigureKestrel(serverOptions =>
{
    serverOptions.ConfigureHttpsDefaults(httpsOptions =>
    {
        var certPath = Path.Combine(builder.Environment.ContentRootPath, "certs", "webview-dev-cert.pfx");
        if (File.Exists(certPath))
        {
            httpsOptions.ServerCertificate = new System.Security.Cryptography.X509Certificates.X509Certificate2(certPath, "");
            Console.WriteLine($"✅ Loaded certificate from {certPath}");
        }
        else
        {
            Console.WriteLine($"⚠️ Certificate not found at {certPath}");
        }
    });
});

// Add authentication
builder.Services.AddAuthentication(options =>
{
    options.DefaultScheme = "Cookies";
    options.DefaultChallengeScheme = "oidc";
})
.AddCookie("Cookies", options =>
{
    options.Cookie.Name = ".AspNetCore.WebViewApp";
    options.Cookie.Domain = "web.dev.internal";
    options.Cookie.SameSite = SameSiteMode.None;
    options.Cookie.SecurePolicy = CookieSecurePolicy.Always;
})
.AddOpenIdConnect("oidc", options =>
{
    options.Authority = "https://idp.dev.internal";
    options.ClientId = "webview-client";
    options.ClientSecret = "webview-secret";
    options.ResponseType = "code";
    options.SaveTokens = true;
    
    options.Scope.Clear();
    options.Scope.Add("openid");
    options.Scope.Add("profile");
    
    options.GetClaimsFromUserInfoEndpoint = true;
    options.MapInboundClaims = false;
    options.RequireHttpsMetadata = true;
    
    // Handle id_token_hint from query string for login hint
    options.Events.OnRedirectToIdentityProvider = context =>
    {
        // Check if id_token_hint was passed in the original request
        if (context.HttpContext.Request.Query.TryGetValue("id_token_hint", out var idTokenHint))
        {
            context.ProtocolMessage.IdTokenHint = idTokenHint;
            // Don't use prompt=none since mobile passkey auth doesn't create browser sessions
            // The id_token_hint will help IdentityServer identify the user
            Console.WriteLine($"[WebView] Using id_token_hint as login hint");
        }
        return Task.CompletedTask;
    };
});

builder.Services.AddAuthorization();

// Add services to the container.
builder.Services.AddRazorPages();

var app = builder.Build();

// Configure the HTTP request pipeline.
if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Error");
    app.UseHsts();
}

app.UseHttpsRedirection();

app.UseRouting();

app.UseAuthentication();
app.UseAuthorization();

app.MapStaticAssets();
app.MapRazorPages()
   .WithStaticAssets()
   .RequireAuthorization();

Console.WriteLine("WebView app starting on https://web.dev.internal:5003");

app.Run();

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

app.MapStaticAssets();
app.MapRazorPages()
   .WithStaticAssets();

Console.WriteLine("WebView app starting on https://web.dev.internal:5003");

app.Run();

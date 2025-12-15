using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Authorization;
using Api.Services;
using Api.Middleware;

var builder = WebApplication.CreateBuilder(args);

// Add DPoP services
builder.Services.AddDistributedMemoryCache();
builder.Services.AddScoped<IReplayCache, ReplayCache>();
builder.Services.AddScoped<DPoPProofValidator>();

// Add services to the container.
builder.Services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
    .AddJwtBearer(options =>
    {
        // Configure the Authority to the IdentityServer instance
        options.Authority = "https://idp.dev.internal";

        // Configure the audience - this should match the API resource or scope
        options.TokenValidationParameters.ValidateAudience = false;

        // For development, you might want to disable HTTPS requirement for metadata
        if (builder.Environment.IsDevelopment())
        {
            options.RequireHttpsMetadata = true;
        }
        
        // Add detailed logging for authentication failures
        options.Events = new JwtBearerEvents
        {
            OnAuthenticationFailed = context =>
            {
                Console.WriteLine($"❌ [Auth] Authentication failed: {context.Exception.Message}");
                Console.WriteLine($"❌ [Auth] Exception type: {context.Exception.GetType().Name}");
                if (context.Exception.InnerException != null)
                {
                    Console.WriteLine($"❌ [Auth] Inner exception: {context.Exception.InnerException.Message}");
                }
                return Task.CompletedTask;
            },
            OnTokenValidated = context =>
            {
                Console.WriteLine($"✅ [Auth] Token validated successfully");
                Console.WriteLine($"✅ [Auth] User: {context.Principal?.Identity?.Name}");
                return Task.CompletedTask;
            },
            OnChallenge = context =>
            {
                Console.WriteLine($"⚠️ [Auth] Challenge issued: {context.Error}, {context.ErrorDescription}");
                return Task.CompletedTask;
            }
        };
    });

builder.Services.AddAuthorization();

var app = builder.Build();

// Configure the HTTP request pipeline.
app.UseHttpsRedirection();

// Add request logging middleware
app.Use(async (context, next) =>
{
    Console.WriteLine($"🌐 [{DateTime.Now:HH:mm:ss}] {context.Request.Method} {context.Request.Path}");
    Console.WriteLine($"🔑 Authorization header: {context.Request.Headers["Authorization"].FirstOrDefault() ?? "NONE"}");
    Console.WriteLine($"🔐 DPoP header: {context.Request.Headers["DPoP"].FirstOrDefault() ?? "NONE"}");
    await next();
    Console.WriteLine($"✅ [{DateTime.Now:HH:mm:ss}] Response: {context.Response.StatusCode}");
});

app.UseAuthentication();
app.UseAuthorization();

// Add DPoP validation middleware (after authentication, before endpoints)
app.UseMiddleware<DPoPValidationMiddleware>();

// Test endpoint for mobile app
app.MapGet("/test", [Authorize] (HttpContext context) =>
{
    var claims = context.User.Claims.Select(c => new
    {
        Type = c.Type,
        Value = c.Value
    });

    return Results.Ok(new
    {
        Message = "API is working!",
        Claims = claims,
        Identity = context.User.Identity?.Name,
        IsAuthenticated = context.User.Identity?.IsAuthenticated ?? false
    });
})
.WithName("Test")
.RequireAuthorization();

// Secured endpoint that returns claims from the access token
app.MapGet("/claims", [Authorize] (HttpContext context) =>
{
    var claims = context.User.Claims.Select(c => new
    {
        Type = c.Type,
        Value = c.Value
    });

    return Results.Ok(new
    {
        Claims = claims,
        Identity = context.User.Identity?.Name,
        IsAuthenticated = context.User.Identity?.IsAuthenticated ?? false
    });
})
.WithName("GetClaims")
.RequireAuthorization();

app.Run();

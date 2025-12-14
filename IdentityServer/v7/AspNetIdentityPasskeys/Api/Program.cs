using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Authorization;

var builder = WebApplication.CreateBuilder(args);

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
    await next();
    Console.WriteLine($"✅ [{DateTime.Now:HH:mm:ss}] Response: {context.Response.StatusCode}");
});

app.UseAuthentication();
app.UseAuthorization();

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

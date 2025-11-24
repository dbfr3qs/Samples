using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Authorization;

var builder = WebApplication.CreateBuilder(args);

// Add services to the container.
builder.Services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
    .AddJwtBearer(options =>
    {
        // Configure the Authority to the IdentityServer instance
        options.Authority = "https://localhost:5001";
        
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

app.UseAuthentication();
app.UseAuthorization();

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

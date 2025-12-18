namespace IdentityServerAspNetIdentityPasskeys.Services;

public interface IMobileSessionStore
{
    Task CreateSessionAsync(MobileSession session);
    Task<MobileSession?> GetSessionByIdAsync(string sessionId);
    Task<MobileSession?> GetSessionByKeyAsync(string key);
    Task DeleteSessionAsync(string sessionId);
}

public class MobileSession
{
    public required string SessionId { get; set; }
    public required string Key { get; set; }
    public required string SubjectId { get; set; }
    public required string DisplayName { get; set; }
    public DateTime Created { get; set; }
    public DateTime Expires { get; set; }
    public Dictionary<string, string> Claims { get; set; } = new();
    
    /// <summary>
    /// DPoP key thumbprint (JKT) that is bound to this mobile session.
    /// Used to verify device binding during session exchange.
    /// The thumbprint is derived from PRF output and is stable per device.
    /// </summary>
    public string? DPoPKeyThumbprint { get; set; }
}

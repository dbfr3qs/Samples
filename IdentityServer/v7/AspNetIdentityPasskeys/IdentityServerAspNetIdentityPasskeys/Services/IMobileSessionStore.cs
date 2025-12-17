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
}

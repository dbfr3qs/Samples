namespace IdentityServerAspNetIdentityPasskeys.Models;

public class PasskeyChallenge
{
    public string Id { get; set; } = Guid.NewGuid().ToString();
    public byte[] Challenge { get; set; } = Array.Empty<byte>();
    public string? UserId { get; set; }
    public string ClientType { get; set; } = string.Empty;
    public DateTime CreatedAt { get; set; }
    public DateTime ExpiresAt { get; set; }
    public bool Used { get; set; }
}

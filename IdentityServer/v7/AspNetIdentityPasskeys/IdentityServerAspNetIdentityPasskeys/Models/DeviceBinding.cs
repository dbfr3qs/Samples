namespace IdentityServerAspNetIdentityPasskeys.Models;

public class DeviceBinding
{
    public int Id { get; set; }
    public string UserId { get; set; } = string.Empty;
    public byte[] CredentialId { get; set; } = Array.Empty<byte>();
    public string DPoPPublicKeyJwk { get; set; } = string.Empty;
    public string PublicKeyThumbprint { get; set; } = string.Empty;
    public string? RefreshTokenHandle { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime LastUsedAt { get; set; }
    public int RefreshCount { get; set; }
}

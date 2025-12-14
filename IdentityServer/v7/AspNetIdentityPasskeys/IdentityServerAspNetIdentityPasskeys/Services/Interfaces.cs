namespace IdentityServerAspNetIdentityPasskeys.Services;

public interface IChallengeStore
{
    Task<string> StoreAsync(ChallengeData challenge);
    Task<ChallengeData?> GetAndRemoveAsync(string challengeId);
    Task CleanupExpiredAsync();
}

public interface ICredentialStore
{
    Task AddAsync(StoredCredential credential);
    Task<StoredCredential?> GetByCredentialIdAsync(byte[] credentialId);
    Task<List<StoredCredential>> GetByUserIdAsync(string userId);
    Task UpdateAsync(StoredCredential credential);
    Task DeleteAsync(byte[] credentialId);
}

public class ChallengeData
{
    public byte[] Challenge { get; set; } = Array.Empty<byte>();
    public string? UserId { get; set; }
    public string ClientType { get; set; } = string.Empty;
    public DateTime CreatedAt { get; set; }
    public DateTime ExpiresAt { get; set; }
    public bool IsExpired => DateTime.UtcNow > ExpiresAt;
}

public class StoredCredential
{
    public string UserId { get; set; } = string.Empty;
    public byte[] CredentialId { get; set; } = Array.Empty<byte>();
    public byte[] PublicKey { get; set; } = Array.Empty<byte>();
    public uint SignatureCounter { get; set; }
    public string CredType { get; set; } = string.Empty;
    public Guid AaGuid { get; set; }
    public string AttestationFormat { get; set; } = string.Empty;
    public string DeviceType { get; set; } = string.Empty;
    public DateTime CreatedAt { get; set; }
    public DateTime? LastUsed { get; set; }
}

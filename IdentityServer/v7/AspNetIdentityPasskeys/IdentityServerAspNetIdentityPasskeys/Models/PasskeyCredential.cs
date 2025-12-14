namespace IdentityServerAspNetIdentityPasskeys.Models;

public class PasskeyCredential
{
    public int Id { get; set; }
    public required string UserId { get; set; }
    public required byte[] CredentialId { get; set; }
    public required byte[] PublicKey { get; set; }
    public uint SignatureCounter { get; set; }
    public required string CredType { get; set; }
    public Guid AaGuid { get; set; }
    public required string AttestationFormat { get; set; }
    public required string DeviceType { get; set; }
    public DateTime CreatedAt { get; set; }
}

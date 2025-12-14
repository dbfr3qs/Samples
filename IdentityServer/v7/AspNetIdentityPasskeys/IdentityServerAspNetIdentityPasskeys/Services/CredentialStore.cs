using IdentityServerAspNetIdentityPasskeys.Data;
using IdentityServerAspNetIdentityPasskeys.Models;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;

namespace IdentityServerAspNetIdentityPasskeys.Services;

public class CredentialStore : ICredentialStore
{
    private readonly ApplicationDbContext _context;
    
    public CredentialStore(ApplicationDbContext context)
    {
        _context = context;
    }
    
    public async Task AddAsync(StoredCredential credential)
    {
        var passkeyCredential = new PasskeyCredential
        {
            UserId = credential.UserId,
            CredentialId = credential.CredentialId,
            PublicKey = credential.PublicKey,
            SignatureCounter = credential.SignatureCounter,
            CredType = credential.CredType,
            AaGuid = credential.AaGuid,
            AttestationFormat = credential.AttestationFormat,
            DeviceType = credential.DeviceType,
            CreatedAt = credential.CreatedAt
        };
        
        _context.PasskeyCredentials.Add(passkeyCredential);
        await _context.SaveChangesAsync();
    }
    
    public async Task<StoredCredential?> GetByCredentialIdAsync(byte[] credentialId)
    {
        var credential = await _context.PasskeyCredentials
            .FirstOrDefaultAsync(c => c.CredentialId.SequenceEqual(credentialId));
        
        if (credential == null)
        {
            return null;
        }
        
        return new StoredCredential
        {
            UserId = credential.UserId,
            CredentialId = credential.CredentialId,
            PublicKey = credential.PublicKey,
            SignatureCounter = credential.SignatureCounter,
            CredType = credential.CredType,
            AaGuid = credential.AaGuid,
            AttestationFormat = credential.AttestationFormat,
            DeviceType = credential.DeviceType,
            CreatedAt = credential.CreatedAt
        };
    }
    
    public async Task<List<StoredCredential>> GetByUserIdAsync(string userId)
    {
        var credentials = await _context.PasskeyCredentials
            .Where(c => c.UserId == userId)
            .ToListAsync();
        
        return credentials.Select(c => new StoredCredential
        {
            UserId = c.UserId,
            CredentialId = c.CredentialId,
            PublicKey = c.PublicKey,
            SignatureCounter = c.SignatureCounter,
            CredType = c.CredType,
            AaGuid = c.AaGuid,
            AttestationFormat = c.AttestationFormat,
            DeviceType = c.DeviceType,
            CreatedAt = c.CreatedAt
        }).ToList();
    }
    
    public async Task UpdateAsync(StoredCredential credential)
    {
        var existingCredential = await _context.PasskeyCredentials
            .FirstOrDefaultAsync(c => c.CredentialId.SequenceEqual(credential.CredentialId));
        
        if (existingCredential == null)
        {
            throw new InvalidOperationException("Credential not found");
        }
        
        existingCredential.SignatureCounter = credential.SignatureCounter;
        existingCredential.PublicKey = credential.PublicKey;
        
        await _context.SaveChangesAsync();
    }
    
    public async Task DeleteAsync(byte[] credentialId)
    {
        var credential = await _context.PasskeyCredentials
            .FirstOrDefaultAsync(c => c.CredentialId.SequenceEqual(credentialId));
        
        if (credential != null)
        {
            _context.PasskeyCredentials.Remove(credential);
            await _context.SaveChangesAsync();
        }
    }
}

using IdentityServerAspNetIdentityPasskeys.Data;
using IdentityServerAspNetIdentityPasskeys.Models;
using Microsoft.EntityFrameworkCore;

namespace IdentityServerAspNetIdentityPasskeys.Services;

public class ChallengeStore : IChallengeStore
{
    private readonly ApplicationDbContext _context;
    
    public ChallengeStore(ApplicationDbContext context)
    {
        _context = context;
    }
    
    public async Task<string> StoreAsync(ChallengeData challenge)
    {
        var entity = new PasskeyChallenge
        {
            Id = Guid.NewGuid().ToString(),
            Challenge = challenge.Challenge,
            UserId = challenge.UserId,
            ClientType = challenge.ClientType,
            CreatedAt = challenge.CreatedAt,
            ExpiresAt = challenge.ExpiresAt,
            Used = false
        };
        
        _context.PasskeyChallenges.Add(entity);
        await _context.SaveChangesAsync();
        
        return entity.Id;
    }
    
    public async Task<ChallengeData?> GetAndRemoveAsync(string challengeId)
    {
        var entity = await _context.PasskeyChallenges
            .FirstOrDefaultAsync(c => c.Id == challengeId && !c.Used);
        
        if (entity == null)
        {
            return null;
        }
        
        entity.Used = true;
        await _context.SaveChangesAsync();
        
        return new ChallengeData
        {
            Challenge = entity.Challenge,
            UserId = entity.UserId,
            ClientType = entity.ClientType,
            CreatedAt = entity.CreatedAt,
            ExpiresAt = entity.ExpiresAt
        };
    }
    
    public async Task CleanupExpiredAsync()
    {
        var expiredChallenges = await _context.PasskeyChallenges
            .Where(c => c.ExpiresAt < DateTime.UtcNow || c.Used)
            .ToListAsync();
        
        _context.PasskeyChallenges.RemoveRange(expiredChallenges);
        await _context.SaveChangesAsync();
    }
}

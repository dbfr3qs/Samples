using System.Collections.Concurrent;

namespace IdentityServerAspNetIdentityPasskeys.Services;

public interface IPrfOutputStore
{
    Task StoreAsync(string challengeId, byte[] prfOutput);
    Task<byte[]?> GetAndRemoveAsync(string challengeId);
}

public class PrfOutputStore : IPrfOutputStore
{
    private readonly ConcurrentDictionary<string, PrfOutputData> _store = new();

    public Task StoreAsync(string challengeId, byte[] prfOutput)
    {
        _store[challengeId] = new PrfOutputData
        {
            PrfOutput = prfOutput,
            ExpiresAt = DateTime.UtcNow.AddMinutes(5)
        };
        return Task.CompletedTask;
    }

    public Task<byte[]?> GetAndRemoveAsync(string challengeId)
    {
        if (_store.TryRemove(challengeId, out var data))
        {
            if (data.IsExpired)
            {
                return Task.FromResult<byte[]?>(null);
            }
            return Task.FromResult<byte[]?>(data.PrfOutput);
        }
        return Task.FromResult<byte[]?>(null);
    }

    private class PrfOutputData
    {
        public byte[] PrfOutput { get; set; } = Array.Empty<byte>();
        public DateTime ExpiresAt { get; set; }
        public bool IsExpired => DateTime.UtcNow > ExpiresAt;
    }
}

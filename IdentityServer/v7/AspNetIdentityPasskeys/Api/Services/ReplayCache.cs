using Microsoft.Extensions.Caching.Distributed;
using System.Text;

namespace Api.Services;

public interface IReplayCache
{
    Task<bool> ExistsAsync(string jti);
    Task AddAsync(string jti, TimeSpan expiration);
}

public class ReplayCache : IReplayCache
{
    private readonly IDistributedCache _cache;

    public ReplayCache(IDistributedCache cache)
    {
        _cache = cache;
    }

    public async Task<bool> ExistsAsync(string jti)
    {
        var key = $"dpop:jti:{jti}";
        var value = await _cache.GetAsync(key);
        return value != null;
    }

    public async Task AddAsync(string jti, TimeSpan expiration)
    {
        var key = $"dpop:jti:{jti}";
        var options = new DistributedCacheEntryOptions
        {
            AbsoluteExpirationRelativeToNow = expiration
        };
        await _cache.SetAsync(key, Encoding.UTF8.GetBytes("1"), options);
    }
}

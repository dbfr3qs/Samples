using System.Collections.Concurrent;

namespace IdentityServerAspNetIdentityPasskeys.Services;

public class MobileSessionStore : IMobileSessionStore
{
    private readonly ConcurrentDictionary<string, MobileSession> _sessionsByKey = new();
    private readonly ConcurrentDictionary<string, string> _keysBySessionId = new();
    private readonly ILogger<MobileSessionStore> _logger;

    public MobileSessionStore(ILogger<MobileSessionStore> logger)
    {
        _logger = logger;
    }

    public Task CreateSessionAsync(MobileSession session)
    {
        _sessionsByKey[session.Key] = session;
        _keysBySessionId[session.SessionId] = session.Key;
        
        _logger.LogInformation("📝 [MobileSessionStore] Created session: SessionId={SessionId}, Key={Key}, Subject={Subject}", 
            session.SessionId, session.Key, session.SubjectId);
        
        return Task.CompletedTask;
    }

    public Task<MobileSession?> GetSessionByIdAsync(string sessionId)
    {
        if (_keysBySessionId.TryGetValue(sessionId, out var key) && 
            _sessionsByKey.TryGetValue(key, out var session))
        {
            if (session.Expires > DateTime.UtcNow)
            {
                _logger.LogInformation("✅ [MobileSessionStore] Found session by ID: {SessionId}", sessionId);
                return Task.FromResult<MobileSession?>(session);
            }
            
            _logger.LogWarning("⚠️ [MobileSessionStore] Session expired: {SessionId}", sessionId);
            // Clean up expired session
            _sessionsByKey.TryRemove(key, out _);
            _keysBySessionId.TryRemove(sessionId, out _);
        }
        
        _logger.LogWarning("❌ [MobileSessionStore] Session not found by ID: {SessionId}", sessionId);
        return Task.FromResult<MobileSession?>(null);
    }

    public Task<MobileSession?> GetSessionByKeyAsync(string key)
    {
        if (_sessionsByKey.TryGetValue(key, out var session))
        {
            if (session.Expires > DateTime.UtcNow)
            {
                _logger.LogInformation("✅ [MobileSessionStore] Found session by Key: {Key}", key);
                return Task.FromResult<MobileSession?>(session);
            }
            
            _logger.LogWarning("⚠️ [MobileSessionStore] Session expired: {SessionId}", session.SessionId);
            // Clean up expired session
            _sessionsByKey.TryRemove(key, out _);
            _keysBySessionId.TryRemove(session.SessionId, out _);
        }
        
        _logger.LogWarning("❌ [MobileSessionStore] Session not found by Key: {Key}", key);
        return Task.FromResult<MobileSession?>(null);
    }

    public Task DeleteSessionAsync(string sessionId)
    {
        if (_keysBySessionId.TryGetValue(sessionId, out var key))
        {
            _sessionsByKey.TryRemove(key, out _);
            _keysBySessionId.TryRemove(sessionId, out _);
            _logger.LogInformation("🗑️ [MobileSessionStore] Deleted session: {SessionId}", sessionId);
        }
        
        return Task.CompletedTask;
    }
}

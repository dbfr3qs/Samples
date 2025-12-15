using Duende.IdentityServer.Models;
using Duende.IdentityServer.Services;

namespace IdentityServerAspNetIdentityPasskeys.Services;

public class DPoPSessionCoordinator : ISessionCoordinationService
{
    private readonly IDeviceBindingStore _deviceBindingStore;
    private readonly ILogger<DPoPSessionCoordinator> _logger;

    public DPoPSessionCoordinator(
        IDeviceBindingStore deviceBindingStore,
        ILogger<DPoPSessionCoordinator> logger)
    {
        _deviceBindingStore = deviceBindingStore;
        _logger = logger;
    }

    public async Task ProcessLogoutAsync(UserSession session)
    {
        _logger.LogInformation("Processing logout for session {SessionId}", session.SessionId);
        await Task.CompletedTask;
    }

    public async Task ProcessExpirationAsync(UserSession session)
    {
        _logger.LogInformation("Processing expiration for session {SessionId}", session.SessionId);
        await Task.CompletedTask;
    }

    public async Task<bool> ValidateSessionAsync(SessionValidationRequest request)
    {
        _logger.LogDebug("Validating session {SessionId}", request.SessionId);
        
        // Track DPoP usage if present
        if (request.Client?.Claims != null)
        {
            var dpopJkt = request.Client.Claims.FirstOrDefault(c => c.Type == "dpop_jkt")?.Value;
            if (!string.IsNullOrEmpty(dpopJkt))
            {
                var binding = await _deviceBindingStore.GetByThumbprintAsync(dpopJkt);
                if (binding != null)
                {
                    _logger.LogDebug("Session validated with DPoP device binding, thumbprint: {Thumbprint}, refresh count: {Count}", 
                        dpopJkt, binding.RefreshCount);
                }
            }
        }
        
        return await Task.FromResult(true);
    }
}

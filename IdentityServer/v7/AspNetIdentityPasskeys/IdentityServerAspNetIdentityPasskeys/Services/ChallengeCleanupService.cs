namespace IdentityServerAspNetIdentityPasskeys.Services;

public class ChallengeCleanupService : BackgroundService
{
    private readonly IServiceProvider _serviceProvider;
    private readonly ILogger<ChallengeCleanupService> _logger;
    
    public ChallengeCleanupService(
        IServiceProvider serviceProvider,
        ILogger<ChallengeCleanupService> logger)
    {
        _serviceProvider = serviceProvider;
        _logger = logger;
    }
    
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                using var scope = _serviceProvider.CreateScope();
                var challengeStore = scope.ServiceProvider.GetRequiredService<IChallengeStore>();
                
                await challengeStore.CleanupExpiredAsync();
                _logger.LogInformation("Cleaned up expired passkey challenges");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error cleaning up expired challenges");
            }
            
            await Task.Delay(TimeSpan.FromHours(1), stoppingToken);
        }
    }
}

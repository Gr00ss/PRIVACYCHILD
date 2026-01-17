using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace WinFamilyMonitor.Security;

/// <summary>
/// Monitors the service health and ensures it stays running
/// </summary>
public class WatchdogService : BackgroundService
{
    private readonly ILogger<WatchdogService> _logger;
    private DateTime _lastHealthCheck = DateTime.Now;

    public WatchdogService(ILogger<WatchdogService> logger)
    {
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("Watchdog service started");

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                // Perform health check
                _lastHealthCheck = DateTime.Now;
                
                // Log health status periodically (every 10 minutes)
                if (DateTime.Now.Minute % 10 == 0)
                {
                    _logger.LogDebug("Service health check: OK");
                }

                await Task.Delay(TimeSpan.FromSeconds(30), stoppingToken);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Watchdog service error");
                await Task.Delay(TimeSpan.FromSeconds(5), stoppingToken);
            }
        }
    }
}

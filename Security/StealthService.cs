using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using System.Diagnostics;
using WinFamilyMonitor.Models;

namespace WinFamilyMonitor.Security;

/// <summary>
/// Provides stealth functionality to hide the service from casual users
/// </summary>
public class StealthService : BackgroundService
{
    private readonly ILogger<StealthService> _logger;
    private readonly bool _stealthEnabled;

    public StealthService(ILogger<StealthService> logger, IOptions<AppConfig> config)
    {
        _logger = logger;
        _stealthEnabled = config.Value.Security.EnableStealth;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        if (!_stealthEnabled)
        {
            _logger.LogInformation("Stealth mode disabled");
            return;
        }

        try
        {
            await ApplyStealthMeasuresAsync();
            _logger.LogInformation("Stealth measures applied successfully");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to apply stealth measures");
        }

        // Keep service running
        await Task.Delay(Timeout.Infinite, stoppingToken);
    }

    private async Task ApplyStealthMeasuresAsync()
    {
        try
        {
            // Set process priority to low to minimize resource usage
            var currentProcess = Process.GetCurrentProcess();
            currentProcess.PriorityClass = ProcessPriorityClass.BelowNormal;

            _logger.LogInformation("Process priority set to BelowNormal");
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Could not set process priority");
        }

        await Task.CompletedTask;
    }
}

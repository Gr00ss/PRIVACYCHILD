using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using System.Diagnostics;
using WinFamilyMonitor.Models;
using WinFamilyMonitor.Services;

namespace WinFamilyMonitor.Monitoring;

/// <summary>
/// Monitors network activity via DNS cache to track website usage
/// </summary>
public class NetworkMonitor : BackgroundService, IActivityMonitor
{
    private readonly ILogger<NetworkMonitor> _logger;
    private readonly IDataService _dataService;
    private readonly HashSet<string> _systemDomains;
    private readonly int _checkInterval;
    private readonly int _saveInterval;
    
    private readonly Dictionary<string, int> _domainUsage = new();
    private DateTime _lastSaveTime = DateTime.Now;
    private readonly HashSet<string> _seenDomains = new();

    public NetworkMonitor(
        ILogger<NetworkMonitor> logger,
        IDataService dataService,
        IOptions<AppConfig> config)
    {
        _logger = logger;
        _dataService = dataService;
        _systemDomains = new HashSet<string>(config.Value.SystemDomains, StringComparer.OrdinalIgnoreCase);
        _checkInterval = config.Value.Monitoring.NetworkCheckIntervalMs;
        _saveInterval = config.Value.Monitoring.DataSaveIntervalMs;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("NetworkMonitor started");

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await CheckDnsCacheAsync();

                // Save data periodically
                if ((DateTime.Now - _lastSaveTime).TotalMilliseconds >= _saveInterval)
                {
                    await SaveAccumulatedDataAsync();
                }

                await Task.Delay(_checkInterval, stoppingToken);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error in NetworkMonitor loop");
                await Task.Delay(5000, stoppingToken);
            }
        }

        // Save data on shutdown
        await SaveAccumulatedDataAsync();
    }

    private async Task CheckDnsCacheAsync()
    {
        try
        {
            using var process = new Process
            {
                StartInfo = new ProcessStartInfo
                {
                    FileName = "powershell.exe",
                    Arguments = "-Command \"Get-DnsClientCache | Where-Object { ($_.Type -eq 1 -or $_.Type -eq 28) -and $_.Status -eq 0 -and $_.Name } | Select-Object -ExpandProperty Name -Unique\"",
                    UseShellExecute = false,
                    RedirectStandardOutput = true,
                    CreateNoWindow = true,
                    WindowStyle = ProcessWindowStyle.Hidden
                }
            };

            process.Start();
            var output = await process.StandardOutput.ReadToEndAsync();
            await process.WaitForExitAsync();

            if (string.IsNullOrWhiteSpace(output))
                return;

            var domains = output.Split(new[] { '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries);
            var checkInterval = _checkInterval / 1000; // Convert to seconds

            foreach (var rawDomain in domains)
            {
                var domain = NormalizeDomain(rawDomain.Trim());
                
                if (string.IsNullOrEmpty(domain) || 
                    _systemDomains.Any(sd => domain.Contains(sd, StringComparison.OrdinalIgnoreCase)))
                    continue;

                // Track time: assume domain was active during check interval
                if (!_domainUsage.ContainsKey(domain))
                    _domainUsage[domain] = 0;
                
                _domainUsage[domain] += checkInterval;
            }
        }
        catch (Exception ex)
        {
            _logger.LogDebug(ex, "Error checking DNS cache");
        }
    }

    private string NormalizeDomain(string domain)
    {
        if (string.IsNullOrEmpty(domain))
            return string.Empty;

        // Remove common prefixes
        domain = domain.Replace("www.", "", StringComparison.OrdinalIgnoreCase);
        
        // Extract main domain (e.g., example.com from api.example.com)
        var parts = domain.Split('.');
        if (parts.Length >= 2)
        {
            // Take last two parts (domain.tld)
            return string.Join(".", parts.TakeLast(2));
        }

        return domain.ToLowerInvariant();
    }

    private async Task SaveAccumulatedDataAsync()
    {
        try
        {
            if (_domainUsage.Count == 0)
                return;

            var today = DateTime.Today;
            foreach (var kvp in _domainUsage)
            {
                await _dataService.SaveActivityAsync(null, kvp.Key, kvp.Value, today);
            }

            _logger.LogInformation("Saved network data for {Count} domains", _domainUsage.Count);
            _domainUsage.Clear();
            _lastSaveTime = DateTime.Now;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to save accumulated network data");
        }
    }

    public override async Task StopAsync(CancellationToken cancellationToken)
    {
        _logger.LogInformation("NetworkMonitor stopping, saving final data...");
        await SaveAccumulatedDataAsync();
        await base.StopAsync(cancellationToken);
    }

    Task IActivityMonitor.StartAsync(CancellationToken cancellationToken) => StartAsync(cancellationToken);
    Task IActivityMonitor.StopAsync(CancellationToken cancellationToken) => StopAsync(cancellationToken);
}

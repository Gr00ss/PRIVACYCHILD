using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using System.Runtime.InteropServices;
using System.Text;
using WinFamilyMonitor.Models;
using WinFamilyMonitor.Services;

namespace WinFamilyMonitor.Monitoring;

/// <summary>
/// Monitors active window/process to track application usage
/// </summary>
public class ProcessMonitor : BackgroundService, IActivityMonitor
{
    [DllImport("user32.dll")]
    private static extern IntPtr GetForegroundWindow();

    [DllImport("user32.dll")]
    private static extern int GetWindowText(IntPtr hWnd, StringBuilder text, int count);

    [DllImport("user32.dll")]
    private static extern uint GetWindowThreadProcessId(IntPtr hWnd, out uint processId);

    private readonly ILogger<ProcessMonitor> _logger;
    private readonly IDataService _dataService;
    private readonly HashSet<string> _systemProcesses;
    private readonly int _checkInterval;
    private readonly int _saveInterval;
    
    private readonly Dictionary<string, int> _appUsage = new();
    private DateTime _lastSaveTime = DateTime.Now;
    private string? _currentApp;
    private DateTime _currentAppStartTime = DateTime.Now;

    public ProcessMonitor(
        ILogger<ProcessMonitor> logger,
        IDataService dataService,
        IOptions<AppConfig> config)
    {
        _logger = logger;
        _dataService = dataService;
        _systemProcesses = new HashSet<string>(config.Value.SystemProcesses, StringComparer.OrdinalIgnoreCase);
        _checkInterval = config.Value.Monitoring.ProcessCheckIntervalMs;
        _saveInterval = config.Value.Monitoring.DataSaveIntervalMs;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("ProcessMonitor started");

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await CheckActiveWindowAsync();

                // Save data periodically
                if ((DateTime.Now - _lastSaveTime).TotalMilliseconds >= _saveInterval)
                {
                    await SaveAccumulatedDataAsync();
                }

                await Task.Delay(_checkInterval, stoppingToken);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error in ProcessMonitor loop");
                await Task.Delay(5000, stoppingToken); // Wait before retry
            }
        }

        // Save data on shutdown
        await SaveAccumulatedDataAsync();
    }

    private async Task CheckActiveWindowAsync()
    {
        try
        {
            var hwnd = GetForegroundWindow();
            if (hwnd == IntPtr.Zero)
                return;

            // Get process ID
            GetWindowThreadProcessId(hwnd, out uint processId);
            if (processId == 0)
                return;

            // Get process name
            var process = System.Diagnostics.Process.GetProcessById((int)processId);
            var processName = process.ProcessName;

            // Filter system processes
            if (_systemProcesses.Contains(processName))
                return;

            // Track time for current app
            var now = DateTime.Now;
            if (_currentApp != null && _currentApp != processName)
            {
                // App changed, record time for previous app
                var seconds = (int)(now - _currentAppStartTime).TotalSeconds;
                if (seconds > 0)
                {
                    if (!_appUsage.ContainsKey(_currentApp))
                        _appUsage[_currentApp] = 0;
                    
                    _appUsage[_currentApp] += seconds;
                }
            }

            _currentApp = processName;
            _currentAppStartTime = now;
        }
        catch (Exception ex)
        {
            _logger.LogDebug(ex, "Error checking active window");
        }
    }

    private async Task SaveAccumulatedDataAsync()
    {
        try
        {
            if (_appUsage.Count == 0)
                return;

            var today = DateTime.Today;
            foreach (var kvp in _appUsage)
            {
                await _dataService.SaveActivityAsync(kvp.Key, null, kvp.Value, today);
            }

            _logger.LogInformation("Saved activity data for {Count} apps", _appUsage.Count);
            _appUsage.Clear();
            _lastSaveTime = DateTime.Now;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to save accumulated data");
        }
    }

    public override async Task StopAsync(CancellationToken cancellationToken)
    {
        _logger.LogInformation("ProcessMonitor stopping, saving final data...");
        await SaveAccumulatedDataAsync();
        await base.StopAsync(cancellationToken);
    }

    Task IActivityMonitor.StartAsync(CancellationToken cancellationToken) => StartAsync(cancellationToken);
    Task IActivityMonitor.StopAsync(CancellationToken cancellationToken) => StopAsync(cancellationToken);
}

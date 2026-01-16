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
                var timeSinceLastSave = (DateTime.Now - _lastSaveTime).TotalMilliseconds;
                if (timeSinceLastSave >= _saveInterval)
                {
                    _logger.LogInformation("Triggering save: {TimeSince}ms >= {Interval}ms", timeSinceLastSave, _saveInterval);
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
            {
                _logger.LogDebug("GetForegroundWindow returned Zero - no active window");
                return;
            }

            // Get process ID
            GetWindowThreadProcessId(hwnd, out uint processId);
            if (processId == 0)
            {
                _logger.LogDebug("GetWindowThreadProcessId returned 0");
                return;
            }

            // Get process name
            var process = System.Diagnostics.Process.GetProcessById((int)processId);
            var processName = process.ProcessName;

            _logger.LogDebug("Active window: {ProcessName} (PID: {ProcessId})", processName, processId);

            // Filter system processes
            if (_systemProcesses.Contains(processName))
            {
                _logger.LogDebug("Skipping system process: {ProcessName}", processName);
                return;
            }

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
                
                // Switch to new app
                _currentApp = processName;
                _currentAppStartTime = now;
            }
            else if (_currentApp == null)
            {
                // First time tracking
                _currentApp = processName;
                _currentAppStartTime = now;
            }
            // else: same app, keep accumulating time
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
            _logger.LogInformation("SaveAccumulatedDataAsync called");

            // Add current app time before saving
            if (_currentApp != null)
            {
                var now = DateTime.Now;
                var seconds = (int)(now - _currentAppStartTime).TotalSeconds;
                _logger.LogInformation("Current app: {App}, Seconds since start: {Seconds}", _currentApp, seconds);
                
                if (seconds > 0)
                {
                    if (!_appUsage.ContainsKey(_currentApp))
                        _appUsage[_currentApp] = 0;
                    
                    _appUsage[_currentApp] += seconds;
                    _currentAppStartTime = now; // Reset timer
                    _logger.LogInformation("Added {Seconds}s to {App}, Total in dict: {Total}s", seconds, _currentApp, _appUsage[_currentApp]);
                }
            }
            else
            {
                _logger.LogInformation("No current app to save - _currentApp is NULL");
            }

            if (_appUsage.Count == 0)
            {
                _logger.LogInformation("No usage data to save - _appUsage.Count = 0");
                _lastSaveTime = DateTime.Now; // Update timer even if no data
                return;
            }

            var today = DateTime.Today;
            _logger.LogDebug("Saving {Count} apps to database", _appUsage.Count);
            
            foreach (var kvp in _appUsage)
            {
                await _dataService.SaveActivityAsync(kvp.Key, null, kvp.Value, today);
                _logger.LogDebug("Saved {App}: {Seconds}s", kvp.Key, kvp.Value);
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

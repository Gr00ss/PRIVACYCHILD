using Microsoft.Extensions.Logging;
using WinFamilyMonitor.Services;

namespace WinFamilyMonitor.Bot;

/// <summary>
/// Generates formatted reports for Telegram bot
/// </summary>
public class ReportGenerator
{
    private readonly ILogger<ReportGenerator> _logger;
    private readonly IDataService _dataService;

    public ReportGenerator(ILogger<ReportGenerator> logger, IDataService dataService)
    {
        _logger = logger;
        _dataService = dataService;
    }

    public async Task<string> GenerateAppsReportAsync()
    {
        try
        {
            _logger.LogInformation("GenerateAppsReportAsync called");
            var apps = await _dataService.GetTodayAppsAsync();
            _logger.LogInformation("Retrieved {Count} apps from database", apps.Count);
            
            if (apps.Count == 0)
            {
                _logger.LogInformation("No apps found for today");
                return "📱 Сегодня приложения не использовались";
            }

            var today = DateTime.Today.ToString("dd.MM.yyyy");
            var report = $"📱 Отчет по приложениям за {today}\n\n";

            var displayedCount = 0;
            foreach (var app in apps)
            {

                report += $"• {app.AppName ?? "Unknown"}: {app.FormattedDuration}\n";
                displayedCount++;
            }

            _logger.LogInformation("Displayed {Count} apps (filtered from {Total})", displayedCount, apps.Count);

            var totalSeconds = apps.Sum(a => a.Seconds);
            var totalHours = totalSeconds / 3600;
            var totalMinutes = (totalSeconds % 3600) / 60;
            var totalSecs = totalSeconds % 60;
            
            var totalText = totalHours > 0 
                ? $"{totalHours}ч {totalMinutes}м"
                : totalMinutes > 0 
                    ? $"{totalMinutes}м {totalSecs}с"
                    : $"{totalSecs}с";
            
            report += $"\nВсего: {totalText}";
            _logger.LogInformation("Total: {Hours}h {Minutes}m ({Seconds}s)", totalHours, totalMinutes, totalSeconds);

            return report;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to generate apps report");
            return "❌ Ошибка при генерации отчета по приложениям";
        }
    }

    public async Task<string> GenerateNetworkReportAsync()
    {
        try
        {
            var domains = await _dataService.GetTodayDomainsAsync();
            
            if (domains.Count == 0)
                return "🌐 Сегодня сетевая активность не зафиксирована";

            var today = DateTime.Today.ToString("dd.MM.yyyy");
            var report = $"🌐 Отчет по сайтам за {today}\n\n";

            foreach (var domain in domains)
            {

                report += $"• {domain.Domain ?? "Unknown"}: {domain.FormattedDuration}\n";
            }

            var totalSeconds = domains.Sum(d => d.Seconds);
            var totalHours = totalSeconds / 3600;
            var totalMinutes = (totalSeconds % 3600) / 60;
            var totalSecs = totalSeconds % 60;
            
            var totalText = totalHours > 0 
                ? $"{totalHours}ч {totalMinutes}м"
                : totalMinutes > 0 
                    ? $"{totalMinutes}м {totalSecs}с"
                    : $"{totalSecs}с";
            
            report += $"\nВсего: {totalText}";

            return report;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to generate network report");
            return "❌ Ошибка при генерации отчета по сайтам";
        }
    }

    public async Task<string> GenerateDailyReportAsync()
    {
        try
        {
            var appsReport = await GenerateAppsReportAsync();
            var networkReport = await GenerateNetworkReportAsync();

            return $"{appsReport}\n\n{networkReport}";
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to generate daily report");
            return "❌ Ошибка при генерации ежедневного отчета";
        }
    }

    private string EscapeMarkdown(string text)
    {
        // Escape special Markdown characters for Telegram
        var specialChars = new[] { '_', '*', '[', ']', '(', ')', '~', '`', '>', '#', '+', '-', '=', '|', '{', '}', '.', '!' };
        
        foreach (var c in specialChars)
        {
            text = text.Replace(c.ToString(), $"\\{c}");
        }
        
        return text;
    }
}

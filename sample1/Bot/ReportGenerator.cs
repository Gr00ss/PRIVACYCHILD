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
            var apps = await _dataService.GetTodayAppsAsync();
            
            if (apps.Count == 0)
                return "📱 Сегодня приложения не использовались";

            var today = DateTime.Today.ToString("dd.MM");
            var report = $"📱 *Отчет по приложениям за {today}*\n\n";

            foreach (var app in apps)
            {
                if (app.Seconds < 60) // Skip apps with less than 1 minute
                    continue;

                report += $"• {EscapeMarkdown(app.AppName ?? "Unknown")}: {app.FormattedDuration}\n";
            }

            var totalSeconds = apps.Sum(a => a.Seconds);
            var totalHours = totalSeconds / 3600;
            var totalMinutes = (totalSeconds % 3600) / 60;
            
            report += $"\n*Всего:* {totalHours}ч {totalMinutes}м";

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

            var today = DateTime.Today.ToString("dd.MM");
            var report = $"🌐 *Отчет по сайтам за {today}*\n\n";

            foreach (var domain in domains)
            {
                if (domain.Seconds < 60) // Skip domains with less than 1 minute
                    continue;

                report += $"• {EscapeMarkdown(domain.Domain ?? "Unknown")}: {domain.FormattedDuration}\n";
            }

            var totalSeconds = domains.Sum(d => d.Seconds);
            var totalHours = totalSeconds / 3600;
            var totalMinutes = (totalSeconds % 3600) / 60;
            
            report += $"\n*Всего:* {totalHours}ч {totalMinutes}м";

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

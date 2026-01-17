using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Telegram.Bot;
using Telegram.Bot.Polling;
using Telegram.Bot.Types;
using Telegram.Bot.Types.Enums;
using Telegram.Bot.Types.ReplyMarkups;
using WinFamilyMonitor.Models;
using WinFamilyMonitor.Services;

namespace WinFamilyMonitor.Bot;

/// <summary>
/// Main Telegram bot service for receiving commands and sending reports
/// </summary>
public class TelegramBotService : BackgroundService
{
    private readonly ILogger<TelegramBotService> _logger;
    private readonly IDataService _dataService;
    private readonly ReportGenerator _reportGenerator;
    private readonly AppConfig _config;
    private TelegramBotClient? _botClient;
    private Timer? _reportTimer;
    private readonly Dictionary<long, int> _failedAttempts = new();
    private DateTime _lastReportDate = DateTime.MinValue;

    public TelegramBotService(
        ILogger<TelegramBotService> logger,
        IDataService dataService,
        ReportGenerator reportGenerator,
        IOptions<AppConfig> config)
    {
        _logger = logger;
        _dataService = dataService;
        _reportGenerator = reportGenerator;
        _config = config.Value;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        try
        {
            if (string.IsNullOrEmpty(_config.Telegram.BotToken) || 
                _config.Telegram.BotToken == "YOUR_BOT_TOKEN_HERE")
            {
                _logger.LogWarning("Telegram bot token not configured, bot service disabled");
                return;
            }

            _botClient = new TelegramBotClient(_config.Telegram.BotToken);
            
            // Verify bot token
            var me = await _botClient.GetMe(stoppingToken);
            _logger.LogInformation("Bot started: @{BotUsername}", me.Username);

            // Start receiving updates
            var receiverOptions = new ReceiverOptions
            {
                AllowedUpdates = new[] { UpdateType.Message, UpdateType.CallbackQuery }
            };

            _botClient.StartReceiving(
                HandleUpdateAsync,
                HandleErrorAsync,
                receiverOptions,
                stoppingToken);

            // Start daily report timer
            StartReportTimer();

            // Keep service running
            await Task.Delay(Timeout.Infinite, stoppingToken);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to start Telegram bot");
        }
    }

    private async Task HandleUpdateAsync(ITelegramBotClient botClient, Update update, CancellationToken cancellationToken)
    {
        try
        {
            if (update.Message is { } message)
            {
                await HandleMessageAsync(message, cancellationToken);
            }
            else if (update.CallbackQuery is { } callbackQuery)
            {
                await HandleCallbackQueryAsync(callbackQuery, cancellationToken);
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error handling update");
        }
    }

    private async Task HandleMessageAsync(Message message, CancellationToken cancellationToken)
    {
        if (_botClient == null || message.Text == null)
            return;

        var chatId = message.Chat.Id;

        // Check authorization
        if (!IsAuthorized(chatId))
        {
            _failedAttempts.TryGetValue(chatId, out var attempts);
            _failedAttempts[chatId] = attempts + 1;

            if (_failedAttempts[chatId] >= _config.Telegram.MaxFailedAttempts)
            {
                _logger.LogWarning("User {ChatId} blocked after {Attempts} failed attempts", chatId, _failedAttempts[chatId]);
                return;
            }

            await _botClient.SendMessage(
                chatId,
                "❌ Доступ запрещен. Этот бот доступен только авторизованным пользователям.",
                cancellationToken: cancellationToken);
            return;
        }

        var command = message.Text.Split(' ')[0].ToLowerInvariant();

        switch (command)
        {
            case "/start":
                await HandleStartCommandAsync(chatId, cancellationToken);
                break;

            case "/apps":
                await HandleAppsCommandAsync(chatId, cancellationToken);
                break;

            case "/network":
                await HandleNetworkCommandAsync(chatId, cancellationToken);
                break;

            case "/settime":
                await HandleSetTimeCommandAsync(chatId, message.Text, cancellationToken);
                break;

            default:
                await _botClient.SendMessage(
                    chatId,
                    "❓ Неизвестная команда. Используйте /start для списка доступных команд.",
                    cancellationToken: cancellationToken);
                break;
        }
    }

    private async Task HandleCallbackQueryAsync(CallbackQuery callbackQuery, CancellationToken cancellationToken)
    {
        if (_botClient == null || callbackQuery.Message == null)
            return;

        var chatId = callbackQuery.Message.Chat.Id;

        switch (callbackQuery.Data)
        {
            case "apps":
                await HandleAppsCommandAsync(chatId, cancellationToken);
                break;

            case "network":
                await HandleNetworkCommandAsync(chatId, cancellationToken);
                break;
        }

        await _botClient.AnswerCallbackQuery(callbackQuery.Id, cancellationToken: cancellationToken);
    }

    private async Task HandleStartCommandAsync(long chatId, CancellationToken cancellationToken)
    {
        if (_botClient == null) return;

        var keyboard = new InlineKeyboardMarkup(new[]
        {
            new[] { InlineKeyboardButton.WithCallbackData("📱 Приложения", "apps") },
            new[] { InlineKeyboardButton.WithCallbackData("🌐 Сеть", "network") }
        });

        var welcomeMessage = @"👋 Добро пожаловать в Family Monitor!

Доступные команды:
• /apps - Отчет по приложениям
• /network - Отчет по сайтам
• /settime HH:mm - Установить время отчетов

Или используйте кнопки ниже:";

        await _botClient.SendMessage(
            chatId,
            welcomeMessage,
            replyMarkup: keyboard,
            cancellationToken: cancellationToken);
    }

    private async Task HandleAppsCommandAsync(long chatId, CancellationToken cancellationToken)
    {
        if (_botClient == null) return;

        var report = await _reportGenerator.GenerateAppsReportAsync();
        
        await _botClient.SendMessage(
            chatId,
            report,
            cancellationToken: cancellationToken);
    }

    private async Task HandleNetworkCommandAsync(long chatId, CancellationToken cancellationToken)
    {
        if (_botClient == null) return;

        var report = await _reportGenerator.GenerateNetworkReportAsync();
        
        await _botClient.SendMessage(
            chatId,
            report,
            cancellationToken: cancellationToken);
    }

    private async Task HandleSetTimeCommandAsync(long chatId, string messageText, CancellationToken cancellationToken)
    {
        if (_botClient == null) return;

        var parts = messageText.Split(' ');
        if (parts.Length < 2)
        {
            await _botClient.SendMessage(
                chatId,
                "❌ Неверный формат. Используйте: /settime HH:mm (например, /settime 19:30)",
                cancellationToken: cancellationToken);
            return;
        }

        var timeStr = parts[1];
        if (TimeSpan.TryParse(timeStr, out var time))
        {
            await _dataService.SetConfigValueAsync("ReportTime", timeStr);
            _config.Telegram.ReportTime = timeStr;
            
            // Restart timer with new time
            StartReportTimer();

            await _botClient.SendMessage(
                chatId,
                $"✅ Время ежедневных отчетов установлено на {timeStr}",
                cancellationToken: cancellationToken);
        }
        else
        {
            await _botClient.SendMessage(
                chatId,
                "❌ Неверный формат времени. Используйте HH:mm (например, 19:30)",
                cancellationToken: cancellationToken);
        }
    }

    private void StartReportTimer()
    {
        _reportTimer?.Dispose();
        
        _reportTimer = new Timer(
            async _ => await CheckAndSendDailyReportAsync(),
            null,
            TimeSpan.Zero,
            TimeSpan.FromMinutes(1)); // Check every minute
    }

    private async Task CheckAndSendDailyReportAsync()
    {
        try
        {
            if (!TimeSpan.TryParse(_config.Telegram.ReportTime, out var reportTime))
                return;

            var now = DateTime.Now;
            var currentTime = now.TimeOfDay;
            
            // Check if report already sent today
            if (_lastReportDate.Date == now.Date)
            {
                _logger.LogDebug("Daily report already sent today");
                return;
            }
            
            // Check if it's time to send report (within 1 minute window)
            if (Math.Abs((currentTime - reportTime).TotalMinutes) < 1)
            {
                _logger.LogInformation("Sending daily report at {Time}", now.ToString("HH:mm:ss"));
                await SendDailyReportToAllUsersAsync();
                _lastReportDate = now; // Mark report as sent for today
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error checking daily report time");
        }
    }

    private async Task SendDailyReportToAllUsersAsync()
    {
        if (_botClient == null)
            return;

        try
        {
            var report = await _reportGenerator.GenerateDailyReportAsync();

            foreach (var userId in _config.Telegram.AuthorizedUsers)
            {
                try
                {
                    await _botClient.SendMessage(
                        userId,
                        $"📊 Ежедневный отчет\n\n{report}");
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Failed to send daily report to user {UserId}", userId);
                }
            }

            _logger.LogInformation("Daily report sent to {Count} users", _config.Telegram.AuthorizedUsers.Count);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to send daily reports");
        }
    }

    private bool IsAuthorized(long chatId)
    {
        return _config.Telegram.AuthorizedUsers.Contains(chatId);
    }

    private Task HandleErrorAsync(ITelegramBotClient botClient, Exception exception, CancellationToken cancellationToken)
    {
        _logger.LogError(exception, "Telegram bot error");
        return Task.CompletedTask;
    }

    public override async Task StopAsync(CancellationToken cancellationToken)
    {
        _reportTimer?.Dispose();
        await base.StopAsync(cancellationToken);
    }
}

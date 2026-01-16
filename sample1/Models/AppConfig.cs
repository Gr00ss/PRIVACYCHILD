namespace WinFamilyMonitor.Models;

public class AppConfig
{
    public TelegramConfig Telegram { get; set; } = new();
    public MonitoringConfig Monitoring { get; set; } = new();
    public DatabaseConfig Database { get; set; } = new();
    public SecurityConfig Security { get; set; } = new();
    public List<string> SystemProcesses { get; set; } = new();
    public List<string> SystemDomains { get; set; } = new();
}

public class TelegramConfig
{
    public string BotToken { get; set; } = string.Empty;
    public List<long> AuthorizedUsers { get; set; } = new();
    public string ReportTime { get; set; } = "19:00";
    public int MaxFailedAttempts { get; set; } = 5;
}

public class MonitoringConfig
{
    public int ProcessCheckIntervalMs { get; set; } = 1000;
    public int NetworkCheckIntervalMs { get; set; } = 5000;
    public int DataSaveIntervalMs { get; set; } = 300000;
    public double MaxCpuPercent { get; set; } = 2.0;
    public int MaxMemoryMB { get; set; } = 50;
}

public class DatabaseConfig
{
    public string FilePath { get; set; } = "activity.db";
    public int DataRetentionDays { get; set; } = 7;
}

public class SecurityConfig
{
    public string EncryptionKey { get; set; } = string.Empty;
    public bool EnableStealth { get; set; } = true;
}

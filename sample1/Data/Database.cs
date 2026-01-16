using Microsoft.Data.Sqlite;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using WinFamilyMonitor.Models;
using WinFamilyMonitor.Services;

namespace WinFamilyMonitor.Data;

/// <summary>
/// SQLite database service for storing activity data
/// </summary>
public class Database : IDataService
{
    private readonly ILogger<Database> _logger;
    private readonly string _connectionString;
    private readonly int _retentionDays;

    public Database(ILogger<Database> logger, IOptions<AppConfig> config)
    {
        _logger = logger;
        
        var dbPath = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.CommonApplicationData),
            "Microsoft", "NetworkDiagnostics", config.Value.Database.FilePath);
        
        _connectionString = $"Data Source={dbPath}";
        _retentionDays = config.Value.Database.DataRetentionDays;
    }

    public async Task InitializeAsync()
    {
        try
        {
            // Ensure directory exists
            var dbPath = new SqliteConnectionStringBuilder(_connectionString).DataSource;
            var directory = Path.GetDirectoryName(dbPath);
            if (directory != null && !Directory.Exists(directory))
            {
                Directory.CreateDirectory(directory);
            }

            using var connection = new SqliteConnection(_connectionString);
            await connection.OpenAsync();

            // Create tables
            var createTablesScript = @"
                CREATE TABLE IF NOT EXISTS Applications (
                    Id INTEGER PRIMARY KEY,
                    Name TEXT NOT NULL UNIQUE
                );

                CREATE TABLE IF NOT EXISTS Domains (
                    Id INTEGER PRIMARY KEY,
                    Name TEXT NOT NULL UNIQUE
                );

                INSERT OR IGNORE INTO Applications (Id, Name) VALUES (-1, '(none)');
                INSERT OR IGNORE INTO Domains (Id, Name) VALUES (-1, '(none)');

                CREATE TABLE IF NOT EXISTS DailyStats (
                    Id INTEGER PRIMARY KEY AUTOINCREMENT,
                    Date TEXT NOT NULL,
                    AppId INTEGER NOT NULL DEFAULT -1,
                    DomainId INTEGER NOT NULL DEFAULT -1,
                    Seconds INTEGER NOT NULL DEFAULT 0,
                    FOREIGN KEY(AppId) REFERENCES Applications(Id),
                    FOREIGN KEY(DomainId) REFERENCES Domains(Id),
                    UNIQUE(Date, AppId, DomainId)
                );

                CREATE TABLE IF NOT EXISTS Configuration (
                    Key TEXT PRIMARY KEY,
                    Value TEXT NOT NULL
                );

                CREATE INDEX IF NOT EXISTS idx_dailystats_date ON DailyStats(Date);
                CREATE INDEX IF NOT EXISTS idx_dailystats_appid ON DailyStats(AppId);
                CREATE INDEX IF NOT EXISTS idx_dailystats_domainid ON DailyStats(DomainId);
            ";

            using var command = new SqliteCommand(createTablesScript, connection);
            await command.ExecuteNonQueryAsync();

            _logger.LogInformation("Database initialized successfully");
            
            // Cleanup old data
            await CleanupOldDataAsync();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to initialize database");
            throw;
        }
    }

    public async Task SaveActivityAsync(string? appName, string? domain, int seconds, DateTime date)
    {
        if (seconds <= 0 || (string.IsNullOrEmpty(appName) && string.IsNullOrEmpty(domain)))
            return;

        try
        {
            using var connection = new SqliteConnection(_connectionString);
            await connection.OpenAsync();

            using var transaction = connection.BeginTransaction();

            long? appId = null;
            long? domainId = null;

            // Get or create app ID
            if (!string.IsNullOrEmpty(appName))
            {
                appId = await GetOrCreateEntityIdAsync(connection, "Applications", appName, transaction);
            }

            // Get or create domain ID
            if (!string.IsNullOrEmpty(domain))
            {
                domainId = await GetOrCreateEntityIdAsync(connection, "Domains", domain, transaction);
            }

            // Insert or update daily stats
            var dateStr = date.ToString("yyyy-MM-dd");
            
            // Use -1 instead of NULL for FOREIGN KEY compatibility
            var appIdValue = appId ?? -1;
            var domainIdValue = domainId ?? -1;
            
            var sql = @"
                INSERT INTO DailyStats (Date, AppId, DomainId, Seconds)
                VALUES (@date, @appId, @domainId, @seconds)
                ON CONFLICT(Date, AppId, DomainId) 
                DO UPDATE SET Seconds = Seconds + @seconds
            ";

            using var command = new SqliteCommand(sql, connection, transaction);
            command.Parameters.AddWithValue("@date", dateStr);
            command.Parameters.AddWithValue("@appId", appIdValue);
            command.Parameters.AddWithValue("@domainId", domainIdValue);
            command.Parameters.AddWithValue("@seconds", seconds);

            await command.ExecuteNonQueryAsync();
            transaction.Commit();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to save activity data");
        }
    }

    public async Task<List<ActivityRecord>> GetTodayAppsAsync()
    {
        var records = new List<ActivityRecord>();
        var today = DateTime.Today.ToString("yyyy-MM-dd");
        _logger.LogInformation("GetTodayAppsAsync called for date: {Date}", today);

        try
        {
            using var connection = new SqliteConnection(_connectionString);
            await connection.OpenAsync();

            var sql = @"
                SELECT a.Name, SUM(ds.Seconds) as TotalSeconds
                FROM DailyStats ds
                JOIN Applications a ON ds.AppId = a.Id
                WHERE ds.Date = @date AND ds.AppId > -1
                GROUP BY a.Name
                ORDER BY TotalSeconds DESC
            ";

            using var command = new SqliteCommand(sql, connection);
            command.Parameters.AddWithValue("@date", today);

            using var reader = await command.ExecuteReaderAsync();
            while (await reader.ReadAsync())
            {
                records.Add(new ActivityRecord
                {
                    AppName = reader.GetString(0),
                    Seconds = reader.GetInt32(1),
                    Date = DateTime.Today
                });
            }
            
            _logger.LogInformation("Found {Count} app records for today", records.Count);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to get today's apps");
        }

        return records;
    }

    public async Task<List<ActivityRecord>> GetTodayDomainsAsync()
    {
        var records = new List<ActivityRecord>();
        var today = DateTime.Today.ToString("yyyy-MM-dd");

        try
        {
            using var connection = new SqliteConnection(_connectionString);
            await connection.OpenAsync();

            var sql = @"
                SELECT d.Name, SUM(ds.Seconds) as TotalSeconds
                FROM DailyStats ds
                JOIN Domains d ON ds.DomainId = d.Id
                WHERE ds.Date = @date AND ds.DomainId > -1
                GROUP BY d.Name
                ORDER BY TotalSeconds DESC
            ";

            using var command = new SqliteCommand(sql, connection);
            command.Parameters.AddWithValue("@date", today);

            using var reader = await command.ExecuteReaderAsync();
            while (await reader.ReadAsync())
            {
                records.Add(new ActivityRecord
                {
                    Domain = reader.GetString(0),
                    Seconds = reader.GetInt32(1),
                    Date = DateTime.Today
                });
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to get today's domains");
        }

        return records;
    }

    public async Task<string?> GetConfigValueAsync(string key)
    {
        try
        {
            using var connection = new SqliteConnection(_connectionString);
            await connection.OpenAsync();

            var sql = "SELECT Value FROM Configuration WHERE Key = @key";
            using var command = new SqliteCommand(sql, connection);
            command.Parameters.AddWithValue("@key", key);

            var result = await command.ExecuteScalarAsync();
            return result?.ToString();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to get config value for key: {Key}", key);
            return null;
        }
    }

    public async Task SetConfigValueAsync(string key, string value)
    {
        try
        {
            using var connection = new SqliteConnection(_connectionString);
            await connection.OpenAsync();

            var sql = @"
                INSERT INTO Configuration (Key, Value)
                VALUES (@key, @value)
                ON CONFLICT(Key) DO UPDATE SET Value = @value
            ";

            using var command = new SqliteCommand(sql, connection);
            command.Parameters.AddWithValue("@key", key);
            command.Parameters.AddWithValue("@value", value);

            await command.ExecuteNonQueryAsync();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to set config value for key: {Key}", key);
        }
    }

    public async Task CleanupOldDataAsync()
    {
        try
        {
            using var connection = new SqliteConnection(_connectionString);
            await connection.OpenAsync();

            var cutoffDate = DateTime.Today.AddDays(-_retentionDays).ToString("yyyy-MM-dd");
            var sql = "DELETE FROM DailyStats WHERE Date < @cutoffDate";

            using var command = new SqliteCommand(sql, connection);
            command.Parameters.AddWithValue("@cutoffDate", cutoffDate);

            var deleted = await command.ExecuteNonQueryAsync();
            if (deleted > 0)
            {
                _logger.LogInformation("Cleaned up {Count} old records", deleted);
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to cleanup old data");
        }
    }

    private async Task<long> GetOrCreateEntityIdAsync(SqliteConnection connection, string tableName, string name, SqliteTransaction transaction)
    {
        // Try to get existing ID
        var selectSql = $"SELECT Id FROM {tableName} WHERE Name = @name";
        using var selectCmd = new SqliteCommand(selectSql, connection, transaction);
        selectCmd.Parameters.AddWithValue("@name", name);

        var result = await selectCmd.ExecuteScalarAsync();
        if (result != null)
        {
            return Convert.ToInt64(result);
        }

        // Insert new entity
        var insertSql = $"INSERT INTO {tableName} (Name) VALUES (@name) RETURNING Id";
        using var insertCmd = new SqliteCommand(insertSql, connection, transaction);
        insertCmd.Parameters.AddWithValue("@name", name);

        var newId = await insertCmd.ExecuteScalarAsync();
        return Convert.ToInt64(newId!);
    }
}

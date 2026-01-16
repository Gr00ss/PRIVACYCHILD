using WinFamilyMonitor.Models;

namespace WinFamilyMonitor.Services;

public interface IDataService
{
    Task InitializeAsync();
    Task SaveActivityAsync(string? appName, string? domain, int seconds, DateTime date);
    Task<List<ActivityRecord>> GetTodayAppsAsync();
    Task<List<ActivityRecord>> GetTodayDomainsAsync();
    Task<string?> GetConfigValueAsync(string key);
    Task SetConfigValueAsync(string key, string value);
    Task CleanupOldDataAsync();
}

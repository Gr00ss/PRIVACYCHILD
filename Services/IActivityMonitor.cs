namespace WinFamilyMonitor.Services;

public interface IActivityMonitor
{
    Task StartAsync(CancellationToken cancellationToken);
    Task StopAsync(CancellationToken cancellationToken);
}

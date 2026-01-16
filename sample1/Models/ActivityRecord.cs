namespace WinFamilyMonitor.Models;

/// <summary>
/// Represents a single activity tracking record
/// </summary>
public class ActivityRecord
{
    public long Id { get; set; }
    public DateTime Date { get; set; }
    public string? AppName { get; set; }
    public string? Domain { get; set; }
    public int Seconds { get; set; }

    public TimeSpan Duration => TimeSpan.FromSeconds(Seconds);

    public string FormattedDuration
    {
        get
        {
            var hours = Seconds / 3600;
            var minutes = (Seconds % 3600) / 60;
            
            if (hours > 0)
                return $"{hours}ч {minutes}м";
            return $"{minutes}м";
        }
    }
}

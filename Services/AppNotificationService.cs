using UrbanPlanToolbox.Models.Interaction;

namespace UrbanPlanToolbox.Services;

/// <summary>Application-window notification source with short-window duplicate suppression.</summary>
public sealed class AppNotificationService
{
    private readonly object _gate = new();
    private readonly Dictionary<string, DateTimeOffset> _recent = new(StringComparer.Ordinal);
    public static AppNotificationService Default { get; } = new();

    public event EventHandler<AppNotification>? NotificationRaised;

    public bool Notify(AppNotification notification)
    {
        ArgumentNullException.ThrowIfNull(notification);
        var key = $"{notification.Kind}\n{notification.Title}\n{notification.Message}";
        var now = DateTimeOffset.UtcNow;
        lock (_gate)
        {
            foreach (var expired in _recent.Where(entry => now - entry.Value > TimeSpan.FromSeconds(5)).Select(entry => entry.Key).ToArray()) _recent.Remove(expired);
            if (_recent.TryGetValue(key, out var previous) && now - previous < TimeSpan.FromSeconds(3)) return false;
            _recent[key] = now;
        }
        NotificationRaised?.Invoke(this, notification);
        return true;
    }
}

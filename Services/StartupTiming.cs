using System.Diagnostics;

namespace UrbanPlanToolbox.Services;

public sealed record StartupTimingPoint(string Name, long ElapsedMilliseconds, int ThreadId);

public sealed class StartupTiming
{
    private readonly Stopwatch _clock = Stopwatch.StartNew();
    private readonly List<StartupTimingPoint> _points = [];
    private readonly object _gate = new();
    public static StartupTiming Default { get; } = new();
    public IReadOnlyList<StartupTimingPoint> Points { get { lock (_gate) return _points.ToArray(); } }
    public void Mark(string name)
    {
        if (string.IsNullOrWhiteSpace(name)) return;
        var point = new StartupTimingPoint(name, _clock.ElapsedMilliseconds, Environment.CurrentManagedThreadId);
        lock (_gate) _points.Add(point);
        AppLogger.Default.Info("Startup", name, $"elapsedMs={point.ElapsedMilliseconds}; threadId={point.ThreadId}");
#if DEBUG
        Debug.WriteLine($"UrbanPlanToolbox startup {point.Name}: {point.ElapsedMilliseconds} ms, thread {point.ThreadId}");
#endif
    }
}

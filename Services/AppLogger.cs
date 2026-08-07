using System.Text.Json;
using System.Text.RegularExpressions;

namespace UrbanPlanToolbox.Services;

public enum AppLogLevel { Info, Warning, Error }

public sealed class AppLogger
{
    private readonly string _logsDirectory;
    private readonly int _maxFiles;
    private readonly object _gate = new();
    public static AppLogger Default { get; } = new(AppDataPathProvider.Default.Paths.LogsDirectory);

    public AppLogger(string logsDirectory, int maxFiles = 7)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(logsDirectory);
        _logsDirectory = Path.GetFullPath(logsDirectory);
        _maxFiles = Math.Max(1, maxFiles);
    }

    public void Info(string component, string eventName, string? message = null) => Write(AppLogLevel.Info, component, eventName, null, message);
    public void Warning(string component, string eventName, string? message = null) => Write(AppLogLevel.Warning, component, eventName, null, message);
    public void Error(string component, string eventName, Exception? exception = null, string? message = null) => Write(AppLogLevel.Error, component, eventName, exception, message);

    public void RunRetention()
    {
        try
        {
            lock (_gate)
            {
                if (!Directory.Exists(_logsDirectory)) return;
                foreach (var file in Directory.EnumerateFiles(_logsDirectory, "*.log")
                    .Select(path => new FileInfo(path)).OrderByDescending(file => file.LastWriteTimeUtc).Skip(_maxFiles)) file.Delete();
            }
        }
        catch { }
    }

    private void Write(AppLogLevel level, string component, string eventName, Exception? exception, string? message)
    {
        try
        {
            var record = new { timestampUtc = DateTimeOffset.UtcNow, level = level.ToString(), component = Sanitize(component, 80), eventName = Sanitize(eventName, 120), message = Sanitize(message, 240), exceptionType = exception?.GetType().FullName };
            var line = JsonSerializer.Serialize(record) + Environment.NewLine;
            lock (_gate)
            {
                Directory.CreateDirectory(_logsDirectory);
                File.AppendAllText(Path.Combine(_logsDirectory, $"app-{DateTime.UtcNow:yyyy-MM-dd}.log"), line, new System.Text.UTF8Encoding(false));
            }
        }
        catch { }
    }

    internal static string? Sanitize(string? value, int maxLength)
    {
        if (string.IsNullOrWhiteSpace(value)) return null;
        var text = value.Replace(Environment.NewLine, " ").Replace('\r', ' ').Replace('\n', ' ');
        text = Regex.Replace(text, @"(?i)(bearer\s+|token|password|secret|api[-_]?key)\s*[:=]\s*[^\s,;]+", "$1[redacted]", RegexOptions.CultureInvariant);
        text = Regex.Replace(text, @"(?i)([A-Za-z]:\\|\\\\)[^\s]+", "[path]", RegexOptions.CultureInvariant);
        text = Regex.Replace(text, @"(?i)(user(name)?|account)\s*[:=]\s*[^\s,;]+", "$1=[redacted]", RegexOptions.CultureInvariant);
        return text.Length <= maxLength ? text : text[..maxLength];
    }
}

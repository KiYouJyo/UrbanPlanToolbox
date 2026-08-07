using System.Text.Json;
using System.Text.Json.Nodes;
using UrbanPlanToolbox.Models;

namespace UrbanPlanToolbox.Services;

public enum AppTheme
{
    System,
    Light,
    Dark
}

public sealed class SettingsService
{
    public const int CurrentSchemaVersion = 1;
    public const int DefaultDecimalPlaces = 2;
    public const bool DefaultAutoCalculate = false;
    private readonly string _filePath;
    public static event EventHandler<AppSettings>? SettingsChanged;
    public SettingsService(string? filePath = null) => _filePath = filePath ?? AppDataPathProvider.Default.Paths.SettingsFilePath;
    public AppSettings Load()
    {
        try
        {
            if (!File.Exists(_filePath)) return CreateDefaults();
            var root = JsonNode.Parse(File.ReadAllText(_filePath)) as JsonObject;
            if (root is null) return CreateDefaults();
            var settings = CreateDefaults();
            TryRead(root, SettingsKeys.SchemaVersion, (int value) => settings.SchemaVersion = value);
            TryRead(root, SettingsKeys.Theme, (string value) => settings.Theme = value);
            TryRead(root, SettingsKeys.DecimalPlaces, (int value) => settings.DecimalPlaces = value);
            TryRead(root, SettingsKeys.AutoCalculate, (bool value) => settings.AutoCalculate = value);
            TryRead(root, SettingsKeys.Language, (string value) => settings.Language = value);
            TryRead(root, SettingsKeys.ProjectMilestoneNotificationsEnabled, (bool? value) => settings.ProjectMilestoneNotificationsEnabled = value);
            TryRead(root, SettingsKeys.ProjectMilestoneReminderRepeatInterval, (MilestoneReminderRepeatInterval value) => settings.ProjectMilestoneReminderRepeatInterval = value);
            TryRead(root, SettingsKeys.FavoriteToolIds, (List<string> value) => settings.FavoriteToolIds = value);
            return Normalize(settings);
        }
        catch (JsonException) { return CreateDefaults(); }
        catch (IOException) { return CreateDefaults(); }
        catch (UnauthorizedAccessException) { return CreateDefaults(); }
    }
    public void Save(AppSettings settings)
    {
        ArgumentNullException.ThrowIfNull(settings);
        Normalize(settings);
        settings.SchemaVersion = CurrentSchemaVersion;
        Directory.CreateDirectory(Path.GetDirectoryName(_filePath)!);
        var temporary = $"{_filePath}.{Guid.NewGuid():N}.tmp";
        try
        {
            // Keep the existing property names for settings-file compatibility.
            File.WriteAllText(temporary, JsonSerializer.Serialize(settings));
            File.Move(temporary, _filePath, overwrite: true);
            SettingsChanged?.Invoke(this, settings);
        }
        finally { if (File.Exists(temporary)) File.Delete(temporary); }
    }
    public AppSettings Update(Action<AppSettings> update)
    {
        ArgumentNullException.ThrowIfNull(update);
        var settings = Load();
        update(settings);
        Save(settings);
        return settings;
    }

    public AppTheme GetTheme() => NormalizeTheme(Load().Theme);

    public static AppTheme NormalizeTheme(string? value) => value?.Trim() switch
    {
        "Light" => AppTheme.Light,
        "Dark" => AppTheme.Dark,
        _ => AppTheme.System
    };

    public static AppSettings CreateDefaults() => new()
    {
        SchemaVersion = CurrentSchemaVersion,
        DecimalPlaces = DefaultDecimalPlaces,
        AutoCalculate = DefaultAutoCalculate,
        Theme = "System",
        Language = LanguagePreference.SystemValue,
        ProjectMilestoneReminderRepeatInterval = MilestoneReminderRepeatInterval.None,
        FavoriteToolIds = []
    };

    public static AppSettings Normalize(AppSettings settings)
    {
        ArgumentNullException.ThrowIfNull(settings);
        settings.SchemaVersion = CurrentSchemaVersion;
        settings.Theme = NormalizeTheme(settings.Theme).ToString();
        settings.Language = LanguagePreference.Normalize(settings.Language);
        settings.DecimalPlaces = Math.Clamp(settings.DecimalPlaces, 0, 6);
        if (!Enum.IsDefined(settings.ProjectMilestoneReminderRepeatInterval))
            settings.ProjectMilestoneReminderRepeatInterval = MilestoneReminderRepeatInterval.None;
        settings.FavoriteToolIds = (settings.FavoriteToolIds ?? [])
            .Where(id => !string.IsNullOrWhiteSpace(id))
            .Distinct(StringComparer.Ordinal)
            .ToList();
        return settings;
    }

    private static void TryRead<T>(JsonObject root, string key, Action<T> assign)
    {
        var item = root.FirstOrDefault(entry => string.Equals(entry.Key, key, StringComparison.OrdinalIgnoreCase));
        if (item.Value is null) return;
        try
        {
            var value = item.Value.Deserialize<T>(new JsonSerializerOptions(JsonSerializerDefaults.Web));
            if (value is not null) assign(value);
        }
        catch (JsonException) { }
        catch (NotSupportedException) { }
    }
}

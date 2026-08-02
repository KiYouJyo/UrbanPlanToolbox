using System.Text.Json;
using UrbanPlanToolbox.Models;

namespace UrbanPlanToolbox.Services;

public sealed class SettingsService
{
    private readonly string _filePath;
    public static event EventHandler<AppSettings>? SettingsChanged;
    public SettingsService(string? filePath = null) => _filePath = filePath ?? AppDataPathProvider.Default.Paths.SettingsFilePath;
    public AppSettings Load()
    {
        try { return File.Exists(_filePath) ? JsonSerializer.Deserialize<AppSettings>(File.ReadAllText(_filePath)) ?? new AppSettings() : new AppSettings(); }
        catch (JsonException) { return new AppSettings(); }
        catch (IOException) { return new AppSettings(); }
        catch (UnauthorizedAccessException) { return new AppSettings(); }
    }
    public void Save(AppSettings settings)
    {
        ArgumentNullException.ThrowIfNull(settings);
        settings.SchemaVersion = 1;
        Directory.CreateDirectory(Path.GetDirectoryName(_filePath)!);
        var temporary = $"{_filePath}.{Guid.NewGuid():N}.tmp";
        try
        {
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
}

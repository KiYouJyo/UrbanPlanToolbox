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
    public void Save(AppSettings settings) { Directory.CreateDirectory(Path.GetDirectoryName(_filePath)!); File.WriteAllText(_filePath, JsonSerializer.Serialize(settings)); SettingsChanged?.Invoke(this, settings); }
    public AppSettings Update(Action<AppSettings> update)
    {
        ArgumentNullException.ThrowIfNull(update);
        var settings = Load();
        update(settings);
        Save(settings);
        return settings;
    }
}

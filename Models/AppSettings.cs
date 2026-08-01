using UrbanPlanToolbox.Services;

namespace UrbanPlanToolbox.Models;

public sealed class AppSettings
{
    public string Theme { get; set; } = "System";
    public int DecimalPlaces { get; set; } = 2;
    public bool AutoCalculate { get; set; }
    public string Language { get; set; } = LanguagePreference.SystemValue;
    public List<string> FavoriteToolIds { get; set; } = [];
}

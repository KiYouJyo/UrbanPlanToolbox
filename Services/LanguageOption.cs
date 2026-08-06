namespace UrbanPlanToolbox.Services;

public sealed record LanguageOption(string Code, string DisplayNameResourceKey);

public sealed class LanguageChangedEventArgs(string previousLanguage, string currentLanguage) : EventArgs
{
    public string PreviousLanguage { get; } = previousLanguage;
    public string CurrentLanguage { get; } = currentLanguage;
}

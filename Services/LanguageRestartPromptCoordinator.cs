namespace UrbanPlanToolbox.Services;

public sealed class LanguageRestartPromptCoordinator
{
    private bool _isOpen;

    public bool TryBegin(string currentLanguage, string selectedLanguage) =>
        !_isOpen && !string.Equals(currentLanguage, selectedLanguage, StringComparison.OrdinalIgnoreCase) && (_isOpen = true);

    public bool Complete(bool restartRequested, IApplicationRestartService restartService)
    {
        ArgumentNullException.ThrowIfNull(restartService);
        try { return !restartRequested || restartService.TryRestart(); }
        finally { _isOpen = false; }
    }
}

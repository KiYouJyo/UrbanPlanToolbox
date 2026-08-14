using UrbanPlanToolbox.Models;

namespace UrbanPlanToolbox.Services;

public enum ReleaseNotesDisplaySource { LocalizedPackage, GitHubReleaseBody, LocalizedEmptyFallback }

public sealed record ReleaseNotesDisplay(string Text, ReleaseNotesDisplaySource Source);

/// <summary>Owns the single UI fallback policy for update notes.</summary>
public static class ReleaseNotesPresentation
{
    public static ReleaseNotesDisplay Resolve(AppUpdateInfo info, string locale, string unavailableText)
    {
        var normalizedLocale = LocalizedReleaseNotesService.NormalizeLocale(locale);
        if (info.LocalizedReleaseNotes?.Notes.GetValueOrDefault(normalizedLocale) is { Items.Count: > 0 } note &&
            note.Items.All(item => !string.IsNullOrWhiteSpace(item)))
            return new(string.Join(Environment.NewLine, note.Items.Select(item => $"- {item}")), ReleaseNotesDisplaySource.LocalizedPackage);

        // A GitHub release body is only admissible for an English UI; other UIs must never silently cross-fallback.
        if (normalizedLocale == "en-US" && !string.IsNullOrWhiteSpace(info.ReleaseNotes))
            return new(info.ReleaseNotes, ReleaseNotesDisplaySource.GitHubReleaseBody);

        return new(unavailableText, ReleaseNotesDisplaySource.LocalizedEmptyFallback);
    }
}

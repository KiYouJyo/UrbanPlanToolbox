namespace UrbanPlanToolbox.Models;

public sealed record LocalizedReleaseNotes(
    int SchemaVersion,
    string Version,
    IReadOnlyDictionary<string, LocalizedReleaseNote> Notes);

public sealed record LocalizedReleaseNote(string Title, IReadOnlyList<string> Items);

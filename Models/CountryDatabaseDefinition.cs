namespace UrbanPlanToolbox.Models;

public sealed record CountryDatabaseDefinition(string Code, string Region, string NameKey, string DescriptionKey);

public static class CountryDatabaseCatalog
{
    // Country-specific boundaries, statistical identifiers and import rules belong to each adapter.
    public static IReadOnlyList<CountryDatabaseDefinition> All { get; } = Array.AsReadOnly(new[]
    {
        new CountryDatabaseDefinition("JP", "Asia", "Database_Japan", "Database_JapanDescription")
    });
}

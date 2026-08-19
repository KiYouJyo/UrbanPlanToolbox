using Xunit;

namespace UrbanPlanToolbox.Tests;

public sealed class ReferenceDataPackIntegrationContractTests
{
    [Fact]
    public void DataPackArchitectureUsesOfficialVersionedCatalogAndResolverInstallerSplit()
    {
        var root = FindRepositoryRoot();
        var catalog = File.ReadAllText(Path.Combine(root, "Services", "DataPackCatalogService.cs"));
        var installer = File.ReadAllText(Path.Combine(root, "Services", "DataPackInstaller.cs"));
        var resolver = File.ReadAllText(Path.Combine(root, "Services", "DataPackResolver.cs"));
        var facade = File.ReadAllText(Path.Combine(root, "Services", "ReferenceDataPackService.cs"));

        Assert.Contains("catalog/catalog-v1.json", catalog, StringComparison.Ordinal);
        Assert.Contains("catalogVersion", catalog, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("KiYouJyo/UrbanPlanToolbox_Data", installer, StringComparison.Ordinal);
        Assert.Contains("releases/download", installer, StringComparison.Ordinal);
        Assert.Contains("manifest.json", installer, StringComparison.Ordinal);
        Assert.Contains("SHA256", installer, StringComparison.Ordinal);
        Assert.Contains("FixedTimeEquals", installer, StringComparison.Ordinal);
        Assert.Contains("path traversal", installer, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("undeclared payload", installer, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("active-pack.json", File.ReadAllText(Path.Combine(root, "Services", "DataPackStateStore.cs")), StringComparison.Ordinal);
        Assert.Contains("RollbackAsync", resolver, StringComparison.Ordinal);
        Assert.Contains("DataPackCatalogService", facade, StringComparison.Ordinal);
        Assert.Contains("DataPackInstaller", facade, StringComparison.Ordinal);
        Assert.Contains("DataPackResolver", facade, StringComparison.Ordinal);
    }

    [Fact]
    public void RedesignedLibrariesDoNotLoadLegacyPackagedDataAtRuntime()
    {
        var root = FindRepositoryRoot();
        foreach (var page in new[]
        {
            "Views/RegulationsIndexPage.xaml.cs",
            "Views/PlanningTerminologyPage.xaml.cs",
            "Views/DesignConceptDictionaryPage.xaml.cs"
        })
        {
            var code = File.ReadAllText(Path.Combine(root, page));
            Assert.Contains("ReferenceDataPackService.Default.LoadActiveAsync(PackId)", code, StringComparison.Ordinal);
            Assert.Contains("ReferenceDataPackPageCoordinator", code, StringComparison.Ordinal);
            Assert.DoesNotContain("LoadPackaged()", code, StringComparison.Ordinal);
        }

        var regulations = File.ReadAllText(Path.Combine(root, "Views", "RegulationsIndexPage.xaml.cs"));
        var terminology = File.ReadAllText(Path.Combine(root, "Views", "PlanningTerminologyPage.xaml.cs"));
        var concepts = File.ReadAllText(Path.Combine(root, "Views", "DesignConceptDictionaryPage.xaml.cs"));
        Assert.DoesNotContain("RegulationsIndexService", regulations, StringComparison.Ordinal);
        Assert.DoesNotContain("PlanningTerminologyService", terminology, StringComparison.Ordinal);
        Assert.DoesNotContain("DesignConceptDictionaryService", concepts, StringComparison.Ordinal);
    }

    [Fact]
    public void V192FactsAndReleaseNotesStayAligned()
    {
        var root = FindRepositoryRoot();
        var project = File.ReadAllText(Path.Combine(root, "UrbanPlanToolbox.csproj"));
        var sideload = File.ReadAllText(Path.Combine(root, "Package.appxmanifest"));
        var store = File.ReadAllText(Path.Combine(root, "Package.Store.appxmanifest"));
        var version = File.ReadAllText(Path.Combine(root, "Services", "AppVersionProvider.cs"));
        var notes = File.ReadAllText(Path.Combine(root, "Assets", "Data", "ReleaseNotes", "1.9.2.json"));

        Assert.Contains("<Version>1.9.2</Version>", project, StringComparison.Ordinal);
        Assert.Contains("Version=\"1.9.2.0\"", sideload, StringComparison.Ordinal);
        Assert.Contains("Version=\"1.9.2.0\"", store, StringComparison.Ordinal);
        Assert.Contains("Version = \"1.9.2\"", version, StringComparison.Ordinal);
        Assert.Contains("\"version\":\"1.9.2\"", notes, StringComparison.Ordinal);
        Assert.Contains("Data Pack 1.0", notes, StringComparison.Ordinal);
    }

    private static string FindRepositoryRoot()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null && (!File.Exists(Path.Combine(directory.FullName, "UrbanPlanToolbox.csproj")) || !Directory.Exists(Path.Combine(directory.FullName, "Views")))) directory = directory.Parent;
        return directory?.FullName ?? throw new DirectoryNotFoundException("Repository root not found.");
    }
}

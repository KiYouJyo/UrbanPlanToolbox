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
        Assert.Contains("raw.githubusercontent.com", catalog, StringComparison.Ordinal);
        Assert.Contains("api.github.com/repos/KiYouJyo/UrbanPlanToolbox_Data/contents/catalog/catalog-v1.json", catalog, StringComparison.Ordinal);
        Assert.Contains("application/vnd.github.raw+json", catalog, StringComparison.Ordinal);
        Assert.Contains("catalog_fallback_used", catalog, StringComparison.Ordinal);
        Assert.Contains("catalog-network-unavailable", catalog, StringComparison.Ordinal);
        Assert.Contains("catalog-invalid", catalog, StringComparison.Ordinal);
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
    public void OfficialPackDownloadRetriesAndFallsBackToGitHubAssetApiBeforeActivation()
    {
        var root = FindRepositoryRoot();
        var installer = File.ReadAllText(Path.Combine(root, "Services", "DataPackInstaller.cs"));

        Assert.Contains("DownloadVerifiedArchiveAsync", installer, StringComparison.Ordinal);
        Assert.Contains("attempt <= 2", installer, StringComparison.Ordinal);
        Assert.Contains("ReleaseApiPrefix", installer, StringComparison.Ordinal);
        Assert.Contains("application/octet-stream", installer, StringComparison.Ordinal);
        Assert.Contains("pack_download_api_fallback_succeeded", installer, StringComparison.Ordinal);
        Assert.Contains("VerifyCatalogDownload(downloadPath, entry)", installer, StringComparison.Ordinal);

        var validateIndex = installer.IndexOf("var validated = await ValidateArchiveAsync(packId, downloadPath", StringComparison.Ordinal);
        var activateIndex = installer.IndexOf("InstallFromFileAsync(packId, downloadPath, \"official\"", StringComparison.Ordinal);
        Assert.True(validateIndex >= 0 && activateIndex > validateIndex, "Catalog/manifest validation must complete before active-pack state is changed.");
    }

    [Fact]
    public void DataPackUiDistinguishesUncheckedAndFailedCatalogStates()
    {
        var root = FindRepositoryRoot();
        var coordinator = File.ReadAllText(Path.Combine(root, "Services", "ReferenceDataPackPageCoordinator.cs"));
        var text = File.ReadAllText(Path.Combine(root, "Services", "ReferenceLibraryText.cs"));

        Assert.Contains("CloudNotChecked", coordinator, StringComparison.Ordinal);
        Assert.Contains("CatalogNetworkUnavailable", coordinator, StringComparison.Ordinal);
        Assert.Contains("CatalogInvalid", coordinator, StringComparison.Ordinal);
        Assert.Contains("CloudNotChecked", text, StringComparison.Ordinal);
        Assert.Contains("raw.githubusercontent.com", text, StringComparison.Ordinal);
        Assert.Contains("GitHub API", text, StringComparison.Ordinal);
    }

    [Fact]
    public void ProfessionalLibraryControlsUseVisibleBackButtonsAnimatedUpdateButtonsAndRightFilterClusters()
    {
        var root = FindRepositoryRoot();
        foreach (var page in new[]
        {
            "Views/RegulationsIndexPage.xaml",
            "Views/PlanningTerminologyPage.xaml",
            "Views/DesignConceptDictionaryPage.xaml"
        })
        {
            var xaml = File.ReadAllText(Path.Combine(root, page));
            Assert.Contains("<HyperlinkButton x:Name=\"BackButton\"", xaml, StringComparison.Ordinal);
            Assert.Contains("AnimatedUpdateButton x:Name=\"HeaderCheckButton\"", xaml, StringComparison.Ordinal);
            Assert.Contains("AnimatedUpdateButton x:Name=\"CheckButton\"", xaml, StringComparison.Ordinal);
            Assert.Contains("x:Name=\"FilterOptionsPanel\"", xaml, StringComparison.Ordinal);
            Assert.Contains("HorizontalAlignment=\"Right\"", xaml, StringComparison.Ordinal);
            Assert.Contains("ReferenceFilterChoiceTemplate", xaml, StringComparison.Ordinal);
            Assert.DoesNotContain("DisplayMemberPath=\"Display\"", xaml, StringComparison.Ordinal);
        }

        var text = File.ReadAllText(Path.Combine(root, "Services", "ReferenceLibraryText.cs"));
        Assert.DoesNotContain("↻ 检查数据更新", text, StringComparison.Ordinal);
        Assert.DoesNotContain("↻ Check data updates", text, StringComparison.Ordinal);

        var button = File.ReadAllText(Path.Combine(root, "Controls", "AnimatedUpdateButton.cs"));
        Assert.Contains("ProgressRing", button, StringComparison.Ordinal);
        Assert.Contains("_clickedBusy && !IsEnabled", button, StringComparison.Ordinal);
        Assert.Contains("ApplyBackButtonChrome", button, StringComparison.Ordinal);
        Assert.Contains("Color.FromArgb(255", button, StringComparison.Ordinal);
        Assert.DoesNotContain("backButton.Background = ResolveBrush(\"CardBackgroundFillColorDefaultBrush\")", button, StringComparison.Ordinal);
    }

    [Fact]
    public void PackagedSoftwareUpdatesBundleAndActivateLatestOfficialDataPacks()
    {
        var root = FindRepositoryRoot();
        var project = File.ReadAllText(Path.Combine(root, "UrbanPlanToolbox.csproj"));
        var sync = File.ReadAllText(Path.Combine(root, "packaging", "Sync-BundledDataPacks.ps1"));
        var facade = File.ReadAllText(Path.Combine(root, "Services", "ReferenceDataPackService.cs"));

        Assert.Contains("SyncLatestBundledReferenceDataPacks", project, StringComparison.Ordinal);
        Assert.Contains("GenerateAppxPackageOnBuild", project, StringComparison.Ordinal);
        Assert.Contains("Sync-BundledDataPacks.ps1", project, StringComparison.Ordinal);
        Assert.Contains("Assets\\DataPacks\\Bundled\\*.uptdata", project, StringComparison.Ordinal);
        Assert.Contains("SkipBundledDataPackSync", project, StringComparison.Ordinal);

        foreach (var packId in new[] { "planning-regulations", "planning-terminology", "design-concepts" })
            Assert.Contains($"'{packId}'", sync, StringComparison.Ordinal);
        Assert.Contains("UrbanPlanToolbox_Data/main/catalog/catalog-v1.json", sync, StringComparison.Ordinal);
        Assert.Contains("Get-FileHash", sync, StringComparison.Ordinal);
        Assert.Contains("SHA-256 mismatch", sync, StringComparison.Ordinal);
        Assert.Contains("Test-ArchiveManifest", sync, StringComparison.Ordinal);

        Assert.Contains("EnsureBundledPackCurrentAsync", facade, StringComparison.Ordinal);
        Assert.Contains("Assets\", \"DataPacks\", \"Bundled", facade, StringComparison.Ordinal);
        Assert.Contains("InstallFromFileAsync(packId, bestPath, \"bundled\"", facade, StringComparison.Ordinal);
        Assert.Contains("bestVersion.CompareTo(currentVersion) <= 0", facade, StringComparison.Ordinal);
    }

    [Fact]
    public void DynamicFilterTaxonomiesHaveJapaneseAndEnglishPresentationMappings()
    {
        var root = FindRepositoryRoot();
        var converter = File.ReadAllText(Path.Combine(root, "Views", "ReferenceFilterLabelConverter.cs"));

        foreach (var sourceValue in new[]
        {
            "中国", "欧盟/欧洲", "国土空间规划体系", "控制性详细规划",
            "中国国土空间制度", "日本国土与都市计划制度", "通用/语境依赖",
            "城市更新", "建筑设计", "站城一体", "社区营造", "触媒", "韧性", "骑行"
        })
        {
            Assert.Contains($"[\"{sourceValue}\"]", converter, StringComparison.Ordinal);
        }

        Assert.Contains("language.StartsWith(\"ja\"", converter, StringComparison.Ordinal);
        Assert.Contains("language.StartsWith(\"en\"", converter, StringComparison.Ordinal);
    }

    [Fact]
    public void PackagedBuildsDeclareInternetClientForDataUpdates()
    {
        var root = FindRepositoryRoot();
        var sideload = File.ReadAllText(Path.Combine(root, "Package.appxmanifest"));
        var store = File.ReadAllText(Path.Combine(root, "Package.Store.appxmanifest"));

        Assert.Contains("<Capability Name=\"internetClient\" />", sideload, StringComparison.Ordinal);
        Assert.Contains("<Capability Name=\"internetClient\" />", store, StringComparison.Ordinal);
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
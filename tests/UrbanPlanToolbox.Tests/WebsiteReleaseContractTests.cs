using System.Text.Json;
using Xunit;

namespace UrbanPlanToolbox.Tests;

public sealed class WebsiteReleaseContractTests
{
    [Fact]
    public void HomepageFallbackMatchesCurrentDistributionStatus()
    {
        var root = FindRepositoryRoot();
        var html = File.ReadAllText(Path.Combine(root, "docs", "index.html"));
        using var status = JsonDocument.Parse(File.ReadAllText(Path.Combine(root, "docs", "project-status.json")));

        var distribution = status.RootElement.GetProperty("distribution");
        var githubVersion = distribution.GetProperty("github").GetProperty("latestPublishedProductVersion").GetString();
        var store = distribution.GetProperty("microsoftStore");
        var storeCandidate = store.GetProperty("submittedPackageVersion").GetString();
        var storePublic = store.GetProperty("publicProductVersion").GetString();

        Assert.False(string.IsNullOrWhiteSpace(githubVersion));
        Assert.False(string.IsNullOrWhiteSpace(storeCandidate));
        Assert.False(string.IsNullOrWhiteSpace(storePublic));

        Assert.Contains($"id=\"hero-version\">v{githubVersion}<", html, StringComparison.Ordinal);
        Assert.Contains($"id=\"github-version\">{githubVersion}<", html, StringComparison.Ordinal);
        Assert.Contains($"id=\"github-version-2\">{githubVersion}<", html, StringComparison.Ordinal);
        Assert.Contains($"id=\"store-candidate\">{storeCandidate}<", html, StringComparison.Ordinal);
        Assert.Contains($"id=\"store-candidate-2\">{storeCandidate}<", html, StringComparison.Ordinal);
        Assert.Contains($"id=\"store-public\">{storePublic}<", html, StringComparison.Ordinal);
        Assert.Contains($"id=\"store-public-2\">{storePublic}<", html, StringComparison.Ordinal);
        Assert.Contains($"id=\"release-title\">UrbanPlanToolbox v{githubVersion}<", html, StringComparison.Ordinal);
        Assert.Contains($"releases/tag/v{githubVersion}", html, StringComparison.Ordinal);
    }

    [Fact]
    public void HomepageStatusRequestsBypassStaleBrowserCache()
    {
        var root = FindRepositoryRoot();
        var html = File.ReadAllText(Path.Combine(root, "docs", "index.html"));

        Assert.Contains("project-status.json?ts=${Date.now()}", html, StringComparison.Ordinal);
        Assert.Contains("release-notes/${githubVersion}.json?ts=${Date.now()}", html, StringComparison.Ordinal);
    }

    private static string FindRepositoryRoot()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null && !File.Exists(Path.Combine(directory.FullName, "UrbanPlanToolbox.slnx"))) directory = directory.Parent;
        return directory?.FullName ?? throw new DirectoryNotFoundException("Repository root not found.");
    }
}

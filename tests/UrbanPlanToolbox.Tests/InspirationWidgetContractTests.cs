using System.Text.Json;
using System.Xml.Linq;
using UrbanPlanToolbox.Models;
using UrbanPlanToolbox.Services;
using Xunit;

namespace UrbanPlanToolbox.Tests;

public sealed class InspirationWidgetContractTests
{
    [Fact]
    public void BothChannelsRegisterWidgetProviderWithoutReplacingToastActivation()
    {
        var root = FindRoot();
        XNamespace com = "http://schemas.microsoft.com/appx/manifest/com/windows10";
        XNamespace uap3 = "http://schemas.microsoft.com/appx/manifest/uap/windows10/3";
        foreach (var name in new[] { "Package.appxmanifest", "Package.Store.appxmanifest" })
        {
            var document = XDocument.Load(Path.Combine(root, name));
            Assert.Contains(document.Descendants(com + "ExeServer"),
                node => (string?)node.Attribute("Arguments") == "--widget-provider");
            Assert.Contains(document.Descendants(com + "ExeServer"),
                node => (string?)node.Attribute("Arguments") == "----AppNotificationActivated:");
            var extension = Assert.Single(document.Descendants(uap3 + "AppExtension")
                .Where(node => (string?)node.Attribute("Name") == "com.microsoft.windows.widgets"));
            Assert.Contains(extension.Descendants(), node => node.Name.LocalName == "Definition" &&
                (string?)node.Attribute("Id") == "InspirationRecorder");
            Assert.Equal("1.9.5.0", document.Descendants()
                .First(node => node.Name.LocalName == "Identity").Attribute("Version")?.Value);
        }
    }

    [Fact]
    public void WidgetIsSelfContainedWithoutDesktopRecorderAction()
    {
        var provider = File.ReadAllText(Path.Combine(FindRoot(), "Widgets", "InspirationWidgetProvider.cs"));
        foreach (var verb in new[] { "\"new\"", "\"load\"", "\"save\"", "\"save-draft\"", "\"delete\"", "\"confirm-delete\"", "\"cancel-delete\"" })
            Assert.Contains(verb, provider);
        Assert.Contains("isMultiline = true", provider);
        Assert.Contains("Input.ChoiceSet", provider);
        Assert.Contains("CustomState", provider);
        Assert.DoesNotContain("Process.Start(", provider);
        Assert.DoesNotContain("LaunchRecorder(", provider);
    }

    [Fact]
    public void VersionAndReleaseAreGatedUntilWindowsVerification()
    {
        using var json = JsonDocument.Parse(File.ReadAllText(Path.Combine(FindRoot(), "release", "release.json")));
        Assert.Equal("1.9.5", AppVersionProvider.Version);
        Assert.Equal("1.9.5", json.RootElement.GetProperty("product").GetProperty("version").GetString());
        Assert.False(json.RootElement.GetProperty("channels").GetProperty("github").GetProperty("publish").GetBoolean());
        Assert.False(json.RootElement.GetProperty("channels").GetProperty("microsoftStore").GetProperty("submit").GetBoolean());
    }

    [Fact]
    public async Task ConcurrentSavesAcrossIndependentServicesRetainAllIdeas()
    {
        if (!OperatingSystem.IsWindows()) return;
        var directory = Path.Combine(Path.GetTempPath(), "upt-widget-" + Guid.NewGuid().ToString("N"));
        var path = Path.Combine(directory, "inspirations.json");
        try
        {
            var services = Enumerable.Range(0, 4).Select(_ => new InspirationService(path)).ToArray();
            var writes = Enumerable.Range(0, 28).Select(index =>
            {
                var now = DateTimeOffset.UtcNow;
                return services[index % services.Length].CreateAsync(new Inspiration
                {
                    Id = Guid.NewGuid(),
                    Title = "Idea " + index,
                    Category = index % 2 == 0 ? InspirationCategory.Design : InspirationCategory.Research,
                    CreatedAt = now,
                    UpdatedAt = now
                });
            });
            Assert.All(await Task.WhenAll(writes), result => Assert.True(result));
            Assert.Equal(28, (await services[0].ListAsync()).Count);
        }
        finally { if (Directory.Exists(directory)) Directory.Delete(directory, true); }
    }

    private static string FindRoot()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null && !File.Exists(Path.Combine(directory.FullName, "UrbanPlanToolbox.slnx")))
            directory = directory.Parent;
        return directory?.FullName ?? throw new DirectoryNotFoundException();
    }
}

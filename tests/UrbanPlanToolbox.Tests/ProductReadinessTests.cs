using System.Runtime.InteropServices;
using UrbanPlanToolbox.Models;
using UrbanPlanToolbox.Services;
using Xunit;

namespace UrbanPlanToolbox.Tests;

public sealed class ProductReadinessTests
{
    [Fact]
    public void DiagnosticsDoNotContainSensitivePathOrUserData()
    {
        var text = DiagnosticsInfoService.Create("failed at C:\\Users\\secret\\project.json");
        Assert.Contains("UrbanPlanToolbox", text);
        Assert.Contains("1.4.3", text);
        Assert.Contains("Data schema version", text);
        Assert.DoesNotContain("C:\\Users", text);
        Assert.Contains(RuntimeInformation.OSArchitecture.ToString(), text);
    }

    [Fact]
    public async Task ClearLocalDataLeavesExternalFolderAndRecreatesAppRoot()
    {
        var root = Path.Combine(Path.GetTempPath(), $"UrbanPlanToolbox-reset-{Guid.NewGuid():N}");
        var external = Path.Combine(Path.GetTempPath(), $"UrbanPlanToolbox-external-{Guid.NewGuid():N}");
        try
        {
            var provider = new AppDataPathProvider(root, ["tool"]);
            provider.EnsureInfrastructureDirectories();
            Directory.CreateDirectory(external);
            await File.WriteAllTextAsync(Path.Combine(root, "settings.json"), "data");
            await File.WriteAllTextAsync(Path.Combine(external, "keep.txt"), "keep");
            Assert.True(await new LocalDataResetService(provider).ResetAsync());
            Assert.False(File.Exists(Path.Combine(root, "settings.json")));
            Assert.True(File.Exists(Path.Combine(external, "keep.txt")));
            Assert.True(Directory.Exists(provider.Paths.LogsDirectory));
        }
        finally
        {
            if (Directory.Exists(root)) Directory.Delete(root, true);
            if (Directory.Exists(external)) Directory.Delete(external, true);
        }
    }
}

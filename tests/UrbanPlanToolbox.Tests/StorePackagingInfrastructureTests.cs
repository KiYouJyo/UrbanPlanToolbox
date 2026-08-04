using Xunit;

namespace UrbanPlanToolbox.Tests;

public sealed class StorePackagingInfrastructureTests
{
    [Fact]
    public void StoreBuildUsesAnIsolatedWorktreeAndValidatesTheProducedPri()
    {
        var root = FindRepositoryRoot();
        var script = File.ReadAllText(Path.Combine(root, "packaging", "Build-StorePackage.ps1"));
        Assert.Contains("SourceCommit must be the current HEAD", script);
        Assert.Contains("status --porcelain", script);
        Assert.Contains("worktree add --detach", script);
        Assert.Contains("worktree remove --force", script);
        Assert.Contains("Test-PackageResourceIdentity.ps1", script);
        Assert.Contains("UapAppxPackageBuildMode=StoreUpload", script);
    }

    [Fact]
    public void PriIdentityValidatorChecksManifestPrimaryMapAndLanguageCandidates()
    {
        var root = FindRepositoryRoot();
        var script = File.ReadAllText(Path.Combine(root, "packaging", "Test-PackageResourceIdentity.ps1"));
        Assert.Contains("Manifest identity mismatch", script);
        Assert.Contains("PRI primary ResourceMap mismatch", script);
        Assert.Contains("AppDisplayName", script);
        Assert.Contains("AppDescription", script);
        Assert.Contains("Language-$language", script);
    }

    private static string FindRepositoryRoot()
    {
        for (var directory = new DirectoryInfo(AppContext.BaseDirectory); directory is not null; directory = directory.Parent)
            if (File.Exists(Path.Combine(directory.FullName, "UrbanPlanToolbox.slnx"))) return directory.FullName;
        throw new DirectoryNotFoundException();
    }
}

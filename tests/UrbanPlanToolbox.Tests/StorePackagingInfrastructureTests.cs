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
        Assert.Contains("AppxBundle=Always", script);
        Assert.Contains("AppxBundlePlatforms=x64", script);
        Assert.DoesNotContain("AppxBundle=Never", script);
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
        Assert.Contains("RequireBundle", script);
        Assert.Contains("Bundle is missing required scale", script);
    }

    [Fact]
    public void StoreWorkflowIsManualOnlyAndUsesThePartnerCenterProduct()
    {
        var root = FindRepositoryRoot();
        var workflow = File.ReadAllText(Path.Combine(root, ".github", "workflows", "publish-microsoft-store.yml"));
        Assert.Contains("workflow_dispatch:", workflow);
        Assert.DoesNotContain("push:", workflow);
        Assert.DoesNotContain("pull_request:", workflow);
        Assert.DoesNotContain("release:", workflow);
        Assert.Contains("STORE_PRODUCT_ID: 9MWDPJG1BHKW", workflow);
        Assert.Contains("submit_for_certification", workflow);
        Assert.Contains("Validate Microsoft Store credentials", workflow);
        Assert.Contains("Configure Microsoft Store Developer CLI", workflow);
        Assert.Contains("Verify Store product access", workflow);
        Assert.Contains("Upload as draft Store submission", workflow);
    }

    [Fact]
    public void StorePackageContractIsPinnedToTheCurrentVersionAndIdentity()
    {
        var root = FindRepositoryRoot();
        var project = File.ReadAllText(Path.Combine(root, "UrbanPlanToolbox.csproj"));
        var githubManifest = File.ReadAllText(Path.Combine(root, "Package.appxmanifest"));
        var storeManifest = File.ReadAllText(Path.Combine(root, "Package.Store.appxmanifest"));
        var script = File.ReadAllText(Path.Combine(root, "packaging", "Build-StorePackage.ps1"));
        Assert.Contains("<Version>1.2.1</Version>", project);
        Assert.Contains("Version=\"1.2.1.0\"", githubManifest);
        Assert.Contains("Name=\"JoKiy.UrbanPlanToolbox\"", storeManifest);
        Assert.Contains("Publisher=\"CN=C4E4B33A-7B77-4121-897C-7D720A5471F8\"", storeManifest);
        Assert.Contains("Version=\"1.2.1.0\"", storeManifest);
        Assert.Contains("PackageVersion -ne '1.2.1.0'", script);
        Assert.Contains("DistributionChannel=Store", script);
        Assert.Contains("URBANPLANTOOLBOX_STORE", project);
    }

    private static string FindRepositoryRoot()
    {
        for (var directory = new DirectoryInfo(AppContext.BaseDirectory); directory is not null; directory = directory.Parent)
            if (File.Exists(Path.Combine(directory.FullName, "UrbanPlanToolbox.slnx"))) return directory.FullName;
        throw new DirectoryNotFoundException();
    }
}

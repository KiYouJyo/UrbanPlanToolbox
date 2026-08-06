using System.Text.RegularExpressions;
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
        Assert.Contains("submit_for_certification == false", workflow);
        Assert.Contains("certification_confirmation", workflow);
        Assert.Contains("Type SUBMIT to confirm committing the existing Store draft", workflow);
        Assert.Contains("Verify existing Store draft is PendingCommit", workflow);
        Assert.Contains("(?i)\\bPendingCommit\\b", workflow);
        Assert.Contains("Submit existing Store draft for certification", workflow);
        Assert.Contains("'submission', 'publish', $env:STORE_PRODUCT_ID", workflow);
        Assert.Contains("--noCommit", workflow);
        Assert.Contains("[Guid]::TryParse($env:TENANT_ID.Trim()", workflow);
        Assert.Contains("[Guid]::TryParse($env:CLIENT_ID.Trim()", workflow);
        Assert.Contains("'--clientId', $env:CLIENT_ID.Trim()", workflow);
        Assert.Contains("'--tenantId', $env:TENANT_ID.Trim()", workflow);
        Assert.Contains("$LASTEXITCODE", workflow);
        Assert.Contains("id: setup_store_cli", workflow);
        Assert.Contains("id: configure_store_cli", workflow);
        Assert.Contains("id: verify_store_access", workflow);
        Assert.Contains("steps.setup_store_cli.outcome == 'success'", workflow);
        Assert.Contains("steps.configure_store_cli.outcome == 'success'", workflow);
        Assert.Contains("steps.verify_store_access.outcome == 'success'", workflow);
        Assert.Contains("--inputDirectory", workflow);
        Assert.Contains("Split-Path -Parent $env:PACKAGE_PATH", workflow);
        Assert.DoesNotContain("--inputFile", workflow);
        Assert.DoesNotContain("'--reset'", workflow);
        Assert.DoesNotContain("echo y", workflow, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("--yes", workflow, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("--no-confirm", workflow, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("\\\"", workflow);
        Assert.DoesNotContain("$env:CLIENT_SECRET.Length", workflow);
        Assert.DoesNotContain("$env:CLIENT_SECRET.Substring", workflow);
        Assert.Contains("Write workflow summary", workflow);

        var notesStart = workflow.IndexOf("- name: Validate and normalize Store release notes", StringComparison.Ordinal);
        var restoreStart = workflow.IndexOf("- name: Restore Release dependencies", StringComparison.Ordinal);
        var notesStep = workflow[notesStart..restoreStart];
        Assert.DoesNotContain("$LASTEXITCODE", notesStep);
        Assert.Contains("Test-Path -LiteralPath $output -PathType Leaf", notesStep);
        Assert.Contains("ConvertFrom-Json -ErrorAction Stop", notesStep);

        var packageStart = workflow.IndexOf("- name: Build validated Store upload package", StringComparison.Ordinal);
        var artifactStart = workflow.IndexOf("- name: Preserve Store deployment artifact", StringComparison.Ordinal);
        var packageStep = workflow[packageStart..artifactStart];
        Assert.DoesNotContain("$LASTEXITCODE", packageStep);
        Assert.Contains("store-package-build.json", packageStep);
        Assert.Contains("metadata.package", packageStep);
        Assert.Contains("metadata.sha256", packageStep);

        var notesUpdateStart = workflow.IndexOf("- name: Update three-language Store release notes", StringComparison.Ordinal);
        var certificationStart = workflow.IndexOf("- name: Validate certification confirmation", StringComparison.Ordinal);
        var notesUpdateStep = workflow[notesUpdateStart..certificationStart];
        Assert.DoesNotContain("$LASTEXITCODE", notesUpdateStep);
        Assert.Contains("submission ID", notesUpdateStep);

        var certificationSubmitStart = workflow.IndexOf("- name: Submit existing Store draft for certification", StringComparison.Ordinal);
        var certificationEnd = workflow.IndexOf("- name: Show current Store submission status", StringComparison.Ordinal);
        var certificationStep = workflow[certificationSubmitStart..certificationEnd];
        Assert.DoesNotContain("PACKAGE_PATH", certificationStep);
        Assert.DoesNotContain("--inputDirectory", certificationStep);
        Assert.DoesNotContain("--inputFile", certificationStep);
        Assert.DoesNotContain("--noCommit", certificationStep);
    }

    [Fact]
    public void StorePackageContractUsesProjectVersionAsTheSingleVersionSource()
    {
        var root = FindRepositoryRoot();
        var project = File.ReadAllText(Path.Combine(root, "UrbanPlanToolbox.csproj"));
        var githubManifest = File.ReadAllText(Path.Combine(root, "Package.appxmanifest"));
        var storeManifest = File.ReadAllText(Path.Combine(root, "Package.Store.appxmanifest"));
        var script = File.ReadAllText(Path.Combine(root, "packaging", "Build-StorePackage.ps1"));
        var workflow = File.ReadAllText(Path.Combine(root, ".github", "workflows", "publish-microsoft-store.yml"));

        var versionMatch = Regex.Match(project, @"<Version>(\d+\.\d+\.\d+)</Version>");
        Assert.True(versionMatch.Success, "UrbanPlanToolbox.csproj must contain a major.minor.patch Version.");
        var productVersion = versionMatch.Groups[1].Value;
        var expectedPackageVersion = $"{productVersion}.0";

        Assert.Contains($"Version=\"{expectedPackageVersion}\"", githubManifest);
        Assert.Contains("Name=\"JoKiy.UrbanPlanToolbox\"", storeManifest);
        Assert.Contains("Publisher=\"CN=C4E4B33A-7B77-4121-897C-7D720A5471F8\"", storeManifest);
        Assert.Contains($"Version=\"{expectedPackageVersion}\"", storeManifest);

        Assert.Contains("$expectedPackageVersion = \"$projectVersion.0\"", workflow);
        Assert.Contains("$notesPath = \"packaging/store-release-notes/$projectVersion.json\"", workflow);
        Assert.DoesNotContain("EXPECTED_PRODUCT_VERSION", workflow);

        Assert.Contains("$expectedPackageVersion = \"$projectVersion.0\"", script);
        Assert.Contains("$PackageVersion -ne $expectedPackageVersion", script);
        Assert.DoesNotContain("PackageVersion -ne '1.3.0.0'", script);
        Assert.Contains("PackageVersion -ne '1.3.1.0'", script);
        Assert.Contains("DistributionChannel=Store", script);
        Assert.Contains("URBANPLANTOOLBOX_STORE", project);

        var releaseNotesPath = Path.Combine(root, "packaging", "store-release-notes", $"{productVersion}.json");
        Assert.True(File.Exists(releaseNotesPath), $"Missing Store release notes for {productVersion}.");
        var releaseNotes = File.ReadAllText(releaseNotesPath);
        Assert.Contains($"\"version\": \"{productVersion}\"", releaseNotes);
    }

    private static string FindRepositoryRoot()
    {
        for (var directory = new DirectoryInfo(AppContext.BaseDirectory); directory is not null; directory = directory.Parent)
            if (File.Exists(Path.Combine(directory.FullName, "UrbanPlanToolbox.slnx"))) return directory.FullName;
        throw new DirectoryNotFoundException();
    }
}

using System.Text.RegularExpressions;
using System.Xml;
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
        Assert.Contains("function Read-XmlDocument", script);
        Assert.Contains("XmlReaderSettings", script);
        Assert.Contains("DtdProcessing = [System.Xml.DtdProcessing]::Prohibit", script);
        Assert.Contains("$settings.XmlResolver = $null", script);
        Assert.Contains("$document.Load($reader)", script);
        Assert.Contains("XML file was not found", script);
        Assert.DoesNotContain("[Text.Encoding]::UTF8.GetString([IO.File]::ReadAllBytes($projectPath))", script);
        Assert.DoesNotContain("[Text.Encoding]::UTF8.GetString([IO.File]::ReadAllBytes($githubManifestPath))", script);
        Assert.DoesNotContain("[Text.Encoding]::UTF8.GetString([IO.File]::ReadAllBytes($manifestPath))", script);
    }

    [Fact]
    public void StorePackagingXmlInputsLoadWithBomSafeSecureReader()
    {
        var root = FindRepositoryRoot();
        var settings = new XmlReaderSettings
        {
            DtdProcessing = DtdProcessing.Prohibit,
            XmlResolver = null
        };

        foreach (var relativePath in new[] { "UrbanPlanToolbox.csproj", "Package.appxmanifest", "Package.Store.appxmanifest" })
        {
            var path = Path.Combine(root, relativePath);
            using var reader = XmlReader.Create(path, settings);
            var document = new XmlDocument { PreserveWhitespace = true, XmlResolver = null };
            document.Load(reader);
            Assert.NotNull(document.DocumentElement);
        }
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
        Assert.Contains("publication_confirmation", workflow);
        Assert.DoesNotContain("submit_for_certification", workflow);
        Assert.DoesNotContain("certification_confirmation", workflow);
        Assert.Contains("PUBLISH ${{ steps.version.outputs.product_version }}", workflow);
        Assert.Contains("Assert-StoreReadyForNewSubmission.ps1", workflow);
        Assert.Contains("Validate Microsoft Store credentials", workflow);
        Assert.Contains("Configure Microsoft Store Developer CLI", workflow);
        Assert.Contains("Verify Store product access", workflow);
        Assert.Contains("Upload Store package without committing", workflow);
        Assert.Contains("Update three-language Store release notes", workflow);
        Assert.Contains("Verify Store draft contents", workflow);
        Assert.Contains("Verify-StoreDraftSubmission.ps1", workflow);
        Assert.Contains("Commit verified Store submission for certification", workflow);
        Assert.Contains("'submission', 'publish', $env:STORE_PRODUCT_ID", workflow);
        Assert.Contains("Verify-StoreSubmissionCommitted.ps1", workflow);
        Assert.Contains("Remove-TransientStoreDraft.ps1", workflow);
        Assert.Contains("--noCommit", workflow);
        Assert.Contains("[Guid]::TryParse($env:TENANT_ID.Trim()", workflow);
        Assert.Contains("[Guid]::TryParse($env:CLIENT_ID.Trim()", workflow);
        Assert.Contains("'--clientId', $env:CLIENT_ID.Trim()", workflow);
        Assert.Contains("'--tenantId', $env:TENANT_ID.Trim()", workflow);
        Assert.Contains("$LASTEXITCODE", workflow);
        Assert.Contains("id: setup_store_cli", workflow);
        Assert.Contains("id: configure_store_cli", workflow);
        Assert.Contains("id: verify_store_access", workflow);
        Assert.DoesNotContain("--inputDirectory", workflow);
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
        Assert.Contains("if: steps.verify_store_draft.outcome == 'success'", workflow);
        Assert.Contains("if: always() && steps.commit_store_submission.outcome != 'skipped'", workflow);
        Assert.Contains("if: failure() && steps.commit_store_submission.outcome == 'skipped'", workflow);
        Assert.Contains("Certification submission requested: true", workflow);

        var uploadStart = workflow.IndexOf("- name: Upload Store package without committing", StringComparison.Ordinal);
        var notesUpdateStart = workflow.IndexOf("- name: Update three-language Store release notes", StringComparison.Ordinal);
        var uploadStep = workflow[uploadStart..notesUpdateStart];
        Assert.Contains("'publish', $env:PACKAGE_PATH", uploadStep);
        Assert.Contains("--noCommit", uploadStep);
        Assert.DoesNotContain("--inputDirectory", uploadStep);
        Assert.DoesNotContain("(Get-Location).Path", uploadStep);
        Assert.Contains("exactly the selected .msixupload file", uploadStep);
        Assert.Contains("Store package SHA-256 does not match build metadata", uploadStep);

        var commitStart = workflow.IndexOf("- name: Commit verified Store submission for certification", StringComparison.Ordinal);
        var commitEnd = workflow.IndexOf("- name: Verify committed Store submission status", StringComparison.Ordinal);
        var commitStep = workflow[commitStart..commitEnd];
        Assert.Contains("submission', 'publish', $env:STORE_PRODUCT_ID", commitStep);
        Assert.DoesNotContain("PACKAGE_PATH", commitStep);
        Assert.DoesNotContain("--noCommit", commitStep);
    }

    [Fact]
    public void StoreOneRunSafetyScriptsProtectPublishedSubmissionAndPendingCommitBoundary()
    {
        var root = FindRepositoryRoot();
        var ready = File.ReadAllText(Path.Combine(root, "packaging", "Assert-StoreReadyForNewSubmission.ps1"));
        var committed = File.ReadAllText(Path.Combine(root, "packaging", "Verify-StoreSubmissionCommitted.ps1"));
        var cleanup = File.ReadAllText(Path.Combine(root, "packaging", "Remove-TransientStoreDraft.ps1"));

        Assert.Contains("PendingApplicationSubmission", ready);
        Assert.Contains("LastPublishedApplicationSubmission", ready);
        Assert.Contains("publication stopped before upload", ready);
        Assert.Contains("PendingCommit", committed);
        Assert.Contains("TimeoutSeconds", committed);
        Assert.Contains("PollIntervalSeconds", committed);
        Assert.Contains("CommitFailed", committed);
        Assert.Contains("PendingCommit", cleanup);
        Assert.Contains("ExpectedPackageVersion", cleanup);
        Assert.Contains("ExpectedPackageFileName", cleanup);
        Assert.Contains("ProtectedPublishedSubmissionId", cleanup);
        Assert.Contains("Refusing to delete protected published submission", cleanup);
        Assert.Contains("-Method Delete", cleanup);
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

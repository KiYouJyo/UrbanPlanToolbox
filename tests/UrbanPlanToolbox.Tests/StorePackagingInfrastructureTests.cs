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
        Assert.Contains("wackExecuted = $false", script);
        Assert.Contains("wackResult = 'NotRun'", script);
        Assert.DoesNotContain("wackReady = $true", script);
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
    public void PriIdentityValidatorChecksManifestPrimaryMapLanguagesAndInstalledSdk()
    {
        var root = FindRepositoryRoot();
        var script = File.ReadAllText(Path.Combine(root, "packaging", "Test-PackageResourceIdentity.ps1"));
        Assert.Contains("Manifest identity mismatch", script);
        Assert.Contains("PRI primary ResourceMap mismatch", script);
        Assert.Contains("AppDisplayName", script);
        Assert.Contains("AppDescription", script);
        Assert.Contains("LANGUAGE-$language", script);
        Assert.Contains("RequireBundle", script);
        Assert.Contains("Bundle is missing required scale", script);
        Assert.Contains("Resolve-MakePriPath", script);
        Assert.Contains("Windows Kits\\10\\bin", script);
        Assert.Contains("Sort-Object Version -Descending", script);
        Assert.DoesNotContain("10.0.26100.0\\x64\\makepri.exe", script);
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
        Assert.DoesNotContain("EXPECTED_LAST_PUBLISHED_SUBMISSION_ID", workflow);
        Assert.DoesNotContain("PROTECTED_PUBLISHED_SUBMISSION_ID", workflow);
        Assert.Contains("publication_confirmation", workflow);
        Assert.Contains("PUBLICATION_CONFIRMATION", workflow);
        Assert.Contains("PUBLISH ${{ steps.version.outputs.product_version }}", workflow);
        Assert.Contains("Store publication source must be the exact origin/main HEAD", workflow);
        Assert.DoesNotContain("merge-base --is-ancestor", workflow);
        Assert.Contains("timeout-minutes: 75", workflow);
        Assert.Contains("Assert-StoreReadyForNewSubmission.ps1", workflow);
        Assert.Contains("-ExpectedPackageVersion '${{ steps.version.outputs.package_version }}'", workflow);
        Assert.Contains("Validate Microsoft Store credentials", workflow);
        Assert.Contains("Configure Microsoft Store Developer CLI", workflow);
        Assert.Contains("Verify Store product access", workflow);
        Assert.Contains("version: v0.3.9", workflow);
        Assert.Contains("Upload Store package without committing", workflow);
        Assert.Contains("Update three-language Store release notes", workflow);
        Assert.Contains("Verify Store draft contents", workflow);
        Assert.Contains("Verify-StoreDraftSubmission.ps1", workflow);
        Assert.Contains("Reverify and commit Store submission for certification", workflow);
        Assert.Contains("commit_attempted=true", workflow);
        Assert.Contains("'submission', 'publish', $env:STORE_PRODUCT_ID", workflow);
        Assert.Contains("Verify-StoreSubmissionCommitted.ps1", workflow);
        Assert.Contains("Remove-TransientStoreDraft.ps1", workflow);
        Assert.Contains("continue-on-error: true", workflow);
        Assert.Contains("--noCommit", workflow);
        Assert.Contains("[Guid]::TryParse($env:TENANT_ID.Trim()", workflow);
        Assert.Contains("[Guid]::TryParse($env:CLIENT_ID.Trim()", workflow);
        Assert.Contains("'--clientId', $env:CLIENT_ID.Trim()", workflow);
        Assert.Contains("'--tenantId', $env:TENANT_ID.Trim()", workflow);
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
        Assert.Contains("Microsoft Store v$productVersion publication", workflow);
        Assert.Contains("Post-commit Store status", workflow);
        Assert.Contains("WACK executed in GitHub Actions", workflow);
        Assert.DoesNotContain("Microsoft Store v1.3.1 publication", workflow);

        var notesUpdateStart = workflow.IndexOf("- name: Update three-language Store release notes", StringComparison.Ordinal);
        var uploadStart = workflow.IndexOf("- name: Upload Store package without committing", StringComparison.Ordinal);
        var uploadStep = workflow[uploadStart..notesUpdateStart];
        Assert.Contains("'publish', $env:PACKAGE_PATH", uploadStep);
        Assert.Contains("--noCommit", uploadStep);
        Assert.DoesNotContain("--inputDirectory", uploadStep);
        Assert.DoesNotContain("(Get-Location).Path", uploadStep);
        Assert.Contains("exactly the selected .msixupload file", uploadStep);
        Assert.Contains("Store package SHA-256 does not match build metadata", uploadStep);

        var commitStart = workflow.IndexOf("- name: Reverify and commit Store submission for certification", StringComparison.Ordinal);
        var commitEnd = workflow.IndexOf("- name: Verify committed Store submission status", StringComparison.Ordinal);
        var commitStep = workflow[commitStart..commitEnd];
        Assert.Contains("Verify-StoreDraftSubmission.ps1", commitStep);
        Assert.Contains("submission', 'publish', $env:STORE_PRODUCT_ID", commitStep);
        Assert.DoesNotContain("PACKAGE_PATH", commitStep);
        Assert.DoesNotContain("--noCommit", commitStep);
    }

    [Fact]
    public void StoreStateScriptsFailClosedAndProtectTheCurrentPublishedSubmission()
    {
        var root = FindRepositoryRoot();
        var ready = File.ReadAllText(Path.Combine(root, "packaging", "Assert-StoreReadyForNewSubmission.ps1"));
        var committed = File.ReadAllText(Path.Combine(root, "packaging", "Verify-StoreSubmissionCommitted.ps1"));
        var draft = File.ReadAllText(Path.Combine(root, "packaging", "Verify-StoreDraftSubmission.ps1"));
        var cleanup = File.ReadAllText(Path.Combine(root, "packaging", "Remove-TransientStoreDraft.ps1"));
        var notes = File.ReadAllText(Path.Combine(root, "packaging", "Update-StoreReleaseNotes.ps1"));

        Assert.Contains("PendingApplicationSubmission", ready);
        Assert.Contains("LastPublishedApplicationSubmission", ready);
        Assert.Contains("Store package version must increase monotonically", ready);
        Assert.Contains("ExpectedPackageVersion", ready);
        Assert.DoesNotContain("ExpectedLastPublishedSubmissionId", ready);

        Assert.Contains("PreProcessingFailed", committed);
        Assert.Contains("CertificationFailed", committed);
        Assert.Contains("PublishFailed", committed);
        Assert.Contains("ReleaseFailed", committed);
        Assert.Contains("unknown post-commit status", committed);
        Assert.Contains("acceptedPostCommitStatuses", committed);

        Assert.Contains("PollIntervalSeconds", draft);
        Assert.Contains("TimeoutSeconds", draft);
        Assert.Contains("multiple copies of the uploaded package", draft);
        Assert.Contains("unexpected package at the target version or newer", draft);
        Assert.Contains("PackageVersionString", draft);
        Assert.Contains("application_package_count", draft);

        Assert.Contains("LastPublishedApplicationSubmission", cleanup);
        Assert.Contains("Refusing to delete current published submission", cleanup);
        Assert.Contains("PendingCommit", cleanup);
        Assert.Contains("ExpectedPackageVersion", cleanup);
        Assert.Contains("ExpectedPackageFileName", cleanup);
        Assert.DoesNotContain("ProtectedPublishedSubmissionId", cleanup);
        Assert.Contains("-Method Delete", cleanup);
        Assert.Contains("was not removed within", cleanup);

        Assert.Contains("did not become available for metadata update", notes);
        Assert.Contains("Store draft must be PendingCommit", notes);
        Assert.Contains("left PendingCommit", notes);
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

        Assert.Contains("release/release.json", workflow);
        Assert.Contains("channels.microsoftStore.submit", workflow);
        Assert.Contains("$expectedPackageVersion = [string]$release.product.packageVersion", workflow);
        Assert.Contains("$projectVersion -ne [string]$release.product.version", workflow);
        Assert.Contains("$notesPath = \"packaging/store-release-notes/$projectVersion.json\"", workflow);
        Assert.DoesNotContain("EXPECTED_PRODUCT_VERSION", workflow);

        Assert.Contains("$expectedPackageVersion = \"$projectVersion.0\"", script);
        Assert.Contains("$PackageVersion -ne $expectedPackageVersion", script);
        Assert.DoesNotMatch(new Regex(@"PackageVersion\s+-ne\s+'\d+\.\d+\.\d+\.\d+'"), script);
        Assert.Contains("DistributionChannel=Store", script);
        Assert.Contains("URBANPLANTOOLBOX_STORE", project);

        var releaseNotesPath = Path.Combine(root, "packaging", "store-release-notes", $"{productVersion}.json");
        Assert.True(File.Exists(releaseNotesPath), $"Missing Store release notes for {productVersion}.");
        using var releaseNotes = System.Text.Json.JsonDocument.Parse(File.ReadAllText(releaseNotesPath));
        Assert.Equal(productVersion, releaseNotes.RootElement.GetProperty("version").GetString());
    }

    private static string FindRepositoryRoot()
    {
        for (var directory = new DirectoryInfo(AppContext.BaseDirectory); directory is not null; directory = directory.Parent)
            if (File.Exists(Path.Combine(directory.FullName, "UrbanPlanToolbox.slnx"))) return directory.FullName;
        throw new DirectoryNotFoundException();
    }
}

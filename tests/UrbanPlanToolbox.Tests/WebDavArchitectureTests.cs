using Xunit;

namespace UrbanPlanToolbox.Tests;

public sealed class WebDavArchitectureTests
{
    private static readonly string RepositoryRoot = FindRepositoryRoot();

    [Fact]
    public void WebDavCredential_IsNotStoredInAppSettingsOrPortableBackupContract()
    {
        var appSettings = Read("Models/AppSettings.cs");
        var backupService = Read("Services/BackupDataService.cs");
        Assert.False(appSettings.Contains("WebDav", StringComparison.OrdinalIgnoreCase));
        Assert.False(backupService.Contains("webdav-profile", StringComparison.OrdinalIgnoreCase));
        Assert.False(backupService.Contains("PasswordVault", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public void WebDavCredential_UsesWindowsCredentialLocker()
    {
        var credentialStore = Read("Services/WebDavCredentialStore.cs");
        Assert.Contains("PasswordVault", credentialStore);
        Assert.Contains("PasswordCredential", credentialStore);
    }

    [Fact]
    public void WebDavUpload_PutsFinalBackupAndVerifiesRemoteObject()
    {
        var client = Read("Services/WebDavClient.cs");
        Assert.Contains("HttpMethod.Put", client);
        Assert.Contains("request.Content.Headers.ContentLength = stream.Length;", client);
        Assert.Contains("VerifyUploadedFileAsync", client);
        Assert.Contains("VerifyWithHeadAsync", client);
        Assert.Contains("VerifyWithPropFindAsync", client);
        Assert.Contains("UploadSizeMismatch", client);
        Assert.Contains("UploadNotVisibleAfterPut", client);
        Assert.DoesNotContain(".uploading", client);
        Assert.DoesNotContain("new(\"MOVE\")", client);
    }

    [Fact]
    public void WebDavListing_UsesProviderTolerantDirectoryParser()
    {
        var client = Read("Services/WebDavClient.cs");
        var parser = Read("Services/WebDavDirectoryListingParser.cs");
        Assert.Contains("WebDavDirectoryListingParser.Parse", client);
        Assert.Contains("displayname", parser);
        Assert.Contains("Name.LocalName", parser);
        Assert.Contains("Uri.UnescapeDataString", parser);
        Assert.Contains("propstat", parser);
    }

    [Fact]
    public void CloudRestore_ReusesBackupInspectionAndImport()
    {
        var cloudService = Read("Services/CloudBackupService.cs");
        Assert.Contains("InspectAsync", cloudService);
        Assert.Contains("ImportAsync", cloudService);
        Assert.Contains("BackupValidationFailed", cloudService);
    }

    [Fact]
    public void WebDavUi_ExposesExplicitRestoreAction()
    {
        var xaml = Read("Controls/WebDavDataManagementControl.xaml");
        var code = Read("Controls/WebDavDataManagementControl.xaml.cs");
        var localization = Read("Services/WebDavLocalization.cs");
        Assert.Contains("WebDavRestoreButton", xaml);
        Assert.Contains("Click=\"OnRestoreFromCloud\"", xaml);
        Assert.Contains("OnRestoreFromCloud", code);
        Assert.Contains("RestoreFromCloud", localization);
        Assert.Contains("NoBackupsAfterCreate", localization);
    }

    [Fact]
    public void LocalDataActions_AreDeclaredInOneHorizontalGridRow()
    {
        var settings = Read("Views/SettingsPage.xaml");
        var start = settings.IndexOf("x:Name=\"DataActions\"", StringComparison.Ordinal);
        var end = settings.IndexOf("</Grid>", start, StringComparison.Ordinal);
        Assert.True(start >= 0 && end > start);
        var block = settings[start..end];
        Assert.Contains("ExportButton", block);
        Assert.Contains("ImportButton", block);
        Assert.Contains("ClearDataButton", block);
        Assert.DoesNotContain("Grid.Row=", block);
        Assert.Contains("Grid.Column=\"0\"", block);
        Assert.Contains("Grid.Column=\"1\"", block);
        Assert.Contains("Grid.Column=\"2\"", block);
    }

    [Fact]
    public void WebDavSettingsControl_IsDeclaredInTheVisibleSettingsTree()
    {
        var xaml = Read("Views/SettingsPage.xaml");
        var codeBehind = Read("Views/SettingsPage.xaml.cs");

        Assert.Contains("<controls:WebDavDataManagementControl x:Name=\"WebDavControl\"/>", xaml);
        Assert.Contains("WebDavControl.SetExternalBusy(busy);", codeBehind);
        Assert.Contains("await WebDavControl.RefreshConfigurationAsync();", codeBehind);
        Assert.DoesNotContain("DataActions.Parent", codeBehind);
        Assert.DoesNotContain("new WebDavDataManagementControl()", codeBehind);
    }

    private static string Read(string relativePath) => File.ReadAllText(Path.Combine(RepositoryRoot, relativePath));

    private static string FindRepositoryRoot()
    {
        for (DirectoryInfo? directory = new(AppContext.BaseDirectory); directory is not null; directory = directory.Parent)
        {
            if (File.Exists(Path.Combine(directory.FullName, "UrbanPlanToolbox.csproj")) &&
                File.Exists(Path.Combine(directory.FullName, "Models", "AppSettings.cs")) &&
                File.Exists(Path.Combine(directory.FullName, "Views", "SettingsPage.xaml")))
                return directory.FullName;
        }
        throw new DirectoryNotFoundException("UrbanPlanToolbox repository root with source files was not found from the test output directory.");
    }
}

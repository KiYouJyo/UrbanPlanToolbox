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
    public void WebDavUpload_UsesTemporaryObjectAndMoveFinalization()
    {
        var client = Read("Services/WebDavClient.cs");
        Assert.Contains("PROPFIND", client);
        Assert.Contains("MKCOL", client);
        Assert.Contains("MOVE", client);
        Assert.Contains(".uploading", client);
        Assert.Contains("Destination", client);
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

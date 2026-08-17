using System.Text.RegularExpressions;
using Xunit;

namespace UrbanPlanToolbox.Tests;

public sealed class V184FigmaRegressionTests
{
    [Fact]
    public void AboutPageKeepsWideCardsCompactAndDescriptive()
    {
        var root = FindRepositoryRoot();
        var xaml = File.ReadAllText(Path.Combine(root, "Views", "AboutPage.xaml"));
        var code = File.ReadAllText(Path.Combine(root, "Views", "AboutPage.xaml.cs"));

        Assert.Contains("MaxWidth=\"1000\"", xaml);
        Assert.Contains("Target=\"AppInformationGrid.RowSpacing\" Value=\"0\"", xaml);
        Assert.Contains("Target=\"ProjectOpenSourceGrid.RowSpacing\" Value=\"0\"", xaml);
        Assert.Contains("Target=\"PrivacyRowGrid.RowSpacing\" Value=\"0\"", xaml);
        Assert.Contains("Target=\"NoticesRowGrid.RowSpacing\" Value=\"0\"", xaml);

        foreach (var name in new[]
        {
            "RepositoryDescriptionText", "ReleasesDescriptionText", "IssuesDescriptionText", "LicenseDescriptionText",
            "PrivacyPolicyTitleText", "PrivacyPolicyDescriptionText", "ThirdPartyTitleText", "ThirdPartyDescriptionText"
        })
            Assert.Contains($"x:Name=\"{name}\"", xaml);

        Assert.DoesNotContain("Text=\"PRIVACY.md\"", xaml);
        Assert.DoesNotContain("Text=\"THIRD-PARTY-NOTICES.md\"", xaml);
        Assert.Contains("代码、版本、问题反馈与许可集中成一组", code);
        Assert.Contains("应用默认离线运行，不要求账户", code);
        Assert.Contains("查看所用开源组件、许可证与必要的版权说明", code);
        Assert.Equal(4, Regex.Matches(xaml, "DescriptionText\"").Count);
    }

    [Fact]
    public void DataManagementMatchesTheTwoPanelFigmaContract()
    {
        var root = FindRepositoryRoot();
        var settings = File.ReadAllText(Path.Combine(root, "Views", "SettingsPage.xaml"));
        var settingsCode = File.ReadAllText(Path.Combine(root, "Views", "SettingsPage.xaml.cs"));
        var webDav = File.ReadAllText(Path.Combine(root, "Controls", "WebDavDataManagementControl.xaml"));
        var webDavCode = File.ReadAllText(Path.Combine(root, "Controls", "WebDavDataManagementControl.xaml.cs"));

        Assert.Contains("本地备份与 WebDAV 云存档并列呈现", settingsCode);
        Assert.Contains("LocalBackupTitle", settings);
        Assert.Contains("LocalBackupStatus", settings);
        Assert.Contains("WebDavStatusLabel", webDav);
        Assert.Contains("WebDavStatusValue", webDav);
        Assert.Contains("CompactBackupStamp", webDavCode);
        Assert.DoesNotContain("WebDavLastBackup", webDav + webDavCode);
        Assert.DoesNotContain("WebDavConnectionStatus", webDav + webDavCode);
    }

    private static string FindRepositoryRoot()
    {
        for (var directory = new DirectoryInfo(AppContext.BaseDirectory); directory is not null; directory = directory.Parent)
            if (File.Exists(Path.Combine(directory.FullName, "UrbanPlanToolbox.slnx"))) return directory.FullName;
        throw new DirectoryNotFoundException();
    }
}
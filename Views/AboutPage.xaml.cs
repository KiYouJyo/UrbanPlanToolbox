using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using UrbanPlanToolbox.Models;
using UrbanPlanToolbox.Services;
using Windows.System;

namespace UrbanPlanToolbox.Views;
public sealed partial class AboutPage : Page
{
    private readonly GitHubUpdateService _updateService = new();
    private readonly CancellationTokenSource _pageLifetime = new();

    public AboutPage()
    {
        InitializeComponent();
        Unloaded += (_, _) => _pageLifetime.Cancel();
    }

    private async void OnOpenRepository(object sender, RoutedEventArgs e)
    {
        if (!await Launcher.LaunchUriAsync(RepositoryLinks.Repository)) await ShowMessageAsync("无法打开项目仓库，请检查默认浏览器设置后重试。", "打开失败");
    }

    private async void OnCheckUpdate(object sender, RoutedEventArgs e)
    {
        CheckUpdateButton.IsEnabled = false;
        CheckUpdateButton.Content = "正在检查…";
        try { await ShowUpdateResultAsync(await _updateService.CheckForUpdatesAsync(AppVersionProvider.GetCurrentVersion(), _pageLifetime.Token)); }
        catch (OperationCanceledException) { }
        finally { if (!_pageLifetime.IsCancellationRequested) { CheckUpdateButton.IsEnabled = true; CheckUpdateButton.Content = "检查更新"; } }
    }

    private async Task ShowUpdateResultAsync(UpdateCheckResult result)
    {
        var local = ToDisplayVersion(result.LocalVersion);
        if (result.Status == UpdateCheckStatus.UpdateAvailable && result.Release is not null && result.RemoteVersion is not null)
        {
            var notes = string.IsNullOrWhiteSpace(result.Release.Body) ? "该版本暂未提供更新说明" : Truncate(result.Release.Body, 800);
            var dialog = new ContentDialog { XamlRoot = XamlRoot, Title = $"发现新版本 {ToDisplayVersion(result.RemoteVersion)}", Content = $"当前版本：{local}\n最新版本：{ToDisplayVersion(result.RemoteVersion)}\n\n更新内容：\n{notes}", PrimaryButtonText = "前往下载", CloseButtonText = "稍后" };
            if (await dialog.ShowAsync() == ContentDialogResult.Primary && !await Launcher.LaunchUriAsync(result.Release.HtmlUrl)) await ShowMessageAsync("无法打开 Release 页面，请检查默认浏览器设置后重试。", "打开失败");
            return;
        }

        var message = result.Status switch
        {
            UpdateCheckStatus.UpToDate => $"当前已是最新版本\n\n当前版本：{local}\n最新版本：{ToDisplayVersion(result.RemoteVersion!)}",
            UpdateCheckStatus.LocalVersionNewer => "当前版本不低于最新正式版本",
            UpdateCheckStatus.NoRelease => "GitHub 尚未发布可用的正式版本。",
            UpdateCheckStatus.ConnectionFailed => "无法连接 GitHub，请检查网络连接后重试。",
            UpdateCheckStatus.TimedOut => "请求超时，请稍后重试。",
            UpdateCheckStatus.RateLimited => "GitHub API 请求次数受限，请稍后重试。",
            UpdateCheckStatus.InvalidRemoteVersion => "无法识别远程版本号。",
            _ => "无法获取更新信息，请稍后重试。"
        };
        await ShowMessageAsync(message, "更新");
    }

    private Task ShowMessageAsync(string message, string title) => new ContentDialog { XamlRoot = XamlRoot, Title = title, Content = message, CloseButtonText = "确定" }.ShowAsync().AsTask();
    private static string ToDisplayVersion(Version version) => $"{version.Major}.{version.Minor}.{version.Build}";
    private static string Truncate(string value, int maxLength) => value.Length <= maxLength ? value : $"{value[..maxLength]}…";
}

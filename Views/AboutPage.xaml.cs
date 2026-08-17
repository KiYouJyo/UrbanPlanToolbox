using System.Runtime.InteropServices;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media.Imaging;
using UrbanPlanToolbox.Models;
using UrbanPlanToolbox.Services;
using UrbanPlanToolbox.ViewModels;
using Windows.Storage;
using Windows.ApplicationModel.DataTransfer;
using Windows.System;

namespace UrbanPlanToolbox.Views;

public sealed partial class AboutPage : Page
{
    private const string PublisherDisplayName = "Jo Kiyō";
    private readonly CancellationTokenSource _pageLifetime = new();
    private readonly ILocalizationService _localization = LocalizationService.Default;
    private readonly DistributionChannelContext _channel = new AppDistributionChannelService().GetContext();
    private readonly UpdateViewModel _updates = UpdateViewModel.GetOrCreateDefault(() => new(AppUpdateServiceFactory.CreateDefault(), new ApplicationRestartService(), new ApplicationRestartRegistrationService()));
    private readonly IReleaseNotesProvider _releaseNotes = LocalizedReleaseNotesService.Default;

    public AboutPage()
    {
        InitializeComponent();
        PopulateApplicationInfo();
        ApplyModernizedSectionText();
        CheckUpdateButtonText.Text = T("Action_CheckForUpdates");
        CopyDiagnosticsButton.Content = T("Action_CopyDiagnostics");
        OpenLogsButton.Content = T("Action_OpenLogsFolder");
        Loaded += OnLoaded;
        Unloaded += OnUnloaded;
        ActualThemeChanged += OnActualThemeChanged;
        RenderUpdate();
    }

    private void ApplyModernizedSectionText()
    {
        ProjectOpenSourceTitleText.Text = L("项目与开源", "プロジェクトとオープンソース", "Project & open source");
        ProjectOpenSourceSummaryText.Text = L(
            "代码、版本、问题反馈与许可集中成一组，减少散落按钮。",
            "コード、バージョン、フィードバック、ライセンスを一つのグループにまとめます。",
            "Keep code, releases, feedback, and licensing together instead of scattering actions across the page.");

        RepositoryTitleText.Text = L("GitHub 仓库", "GitHub リポジトリ", "GitHub repository");
        RepositoryDescriptionText.Text = L("查看源码、README 与开发进度。", "ソースコード、README、開発状況を確認します。", "View source code, the README, and development progress.");
        RepositoryButton.Content = L("打开仓库", "リポジトリを開く", "Open repository");

        ReleasesTitleText.Text = "Releases";
        ReleasesDescriptionText.Text = L("查看历史版本、安装包与发行说明。", "過去のバージョン、インストーラー、リリースノートを確認します。", "View version history, installers, and release notes.");
        ReleasesButton.Content = L("查看版本", "リリースを見る", "View releases");

        IssuesTitleText.Text = "Issues";
        IssuesDescriptionText.Text = L("提交缺陷、建议与功能需求。", "不具合、提案、機能要望を送信します。", "Submit bugs, suggestions, and feature requests.");
        IssuesButton.Content = L("打开 Issues", "Issues を開く", "Open Issues");

        LicenseTitleText.Text = L("开源许可", "オープンソースライセンス", "Open-source license");
        LicenseDescriptionText.Text = L("查看项目许可证与第三方授权边界。", "プロジェクトのライセンスと第三者ライセンスの範囲を確認します。", "Review the project license and third-party licensing boundaries.");
        LicenseButton.Content = L("查看许可", "ライセンスを見る", "View license");

        PrivacyLegalTitleText.Text = L("隐私与法律", "プライバシーと法的情報", "Privacy & legal");
        PrivacyLegalSummaryText.Text = L(
            "隐私政策和第三方声明放在页面末尾，低频但始终可达。",
            "プライバシーポリシーと第三者声明をページ末尾にまとめ、必要なときにいつでも確認できます。",
            "Privacy policy and third-party notices stay at the end of the page: low-frequency, but always reachable.");
        PrivacyPolicyTitleText.Text = L("隐私政策", "プライバシーポリシー", "Privacy policy");
        PrivacyPolicyDescriptionText.Text = L(
            "应用默认离线运行，不要求账户；联网行为仅在用户主动触发时发生。",
            "アプリは既定でオフライン動作し、アカウントは不要です。ネットワーク通信はユーザーが明示的に操作した場合のみ発生します。",
            "The app is offline by default and requires no account; network access occurs only when the user explicitly initiates it.");
        PrivacyButton.Content = L("查看隐私政策", "プライバシーポリシーを見る", "View privacy policy");

        ThirdPartyTitleText.Text = L("第三方声明", "第三者声明", "Third-party notices");
        ThirdPartyDescriptionText.Text = L(
            "查看所用开源组件、许可证与必要的版权说明。",
            "使用しているオープンソースコンポーネント、ライセンス、必要な著作権表示を確認します。",
            "Review open-source components, licenses, and required copyright notices.");
        NoticesButton.Content = L("查看第三方声明", "第三者声明を見る", "View third-party notices");
    }

    private async void OnLoaded(object sender, RoutedEventArgs e)
    {
        _updates.PropertyChanged -= OnUpdateStateChanged;
        _updates.PropertyChanged += OnUpdateStateChanged;
        UpdateProductLogo();
        RenderUpdate();
        if (!string.IsNullOrWhiteSpace(_updates.Info.AvailableVersion))
            await _updates.SetLocalizedNotesAsync(_releaseNotes, _localization.CurrentLanguage);
    }

    private void OnUnloaded(object sender, RoutedEventArgs e)
    {
        _updates.PropertyChanged -= OnUpdateStateChanged;
        _pageLifetime.Cancel();
    }

    private void OnUpdateStateChanged(object? sender, System.ComponentModel.PropertyChangedEventArgs e) => DispatcherQueue.TryEnqueue(RenderUpdate);

    private void OnActualThemeChanged(FrameworkElement sender, object e) => UpdateProductLogo();

    private void UpdateProductLogo()
    {
        var theme = ActualTheme == ElementTheme.Dark ? AppTheme.Dark : AppTheme.Light;
        ProductLogo.Source = new BitmapImage(new Uri(WindowIconTheme.GetLogoUri(theme)));
    }

    private void PopulateApplicationInfo()
    {
        DisplayVersionText.Text = AppVersionProvider.DisplayVersion;
        PackageVersionText.Text = AppVersionProvider.GetPackageVersion();
        ArchitectureText.Text = RuntimeInformation.ProcessArchitecture.ToString();
        ChannelText.Text = ChannelLabel(_channel);
        UpdateSourceText.Text = ChannelLabel(_channel);
        UpdateVersionText.Text = AppVersionProvider.DisplayVersion;
        PublisherText.Text = PublisherDisplayName;
    }

    private async void OnOpenRepository(object sender, RoutedEventArgs e) => await OpenLinkAsync(RepositoryLinks.Repository);
    private async void OnOpenReleases(object sender, RoutedEventArgs e) => await OpenLinkAsync(RepositoryLinks.Releases);
    private async void OnOpenIssues(object sender, RoutedEventArgs e) => await OpenLinkAsync(RepositoryLinks.Issues);
    private async void OnOpenLicense(object sender, RoutedEventArgs e) => await OpenDocumentAsync("LICENSE");
    private async void OnOpenPrivacy(object sender, RoutedEventArgs e) => await OpenDocumentAsync("PRIVACY.md");
    private async void OnOpenNotices(object sender, RoutedEventArgs e) => await OpenDocumentAsync("THIRD-PARTY-NOTICES.md");

    private async void OnCheckUpdate(object sender, RoutedEventArgs e)
    {
        if (_updates.Info.NeedsFinalRestart)
        {
            await _updates.SetLocalizedNotesAsync(_releaseNotes, _localization.CurrentLanguage);
            await _updates.RestartAndUpdateAsync();
            return;
        }
        if (_updates.Info.IsUpdateAvailable)
        {
            await _updates.SetLocalizedNotesAsync(_releaseNotes, _localization.CurrentLanguage);
            await _updates.DownloadAndInstallAsync();
            return;
        }
        await _updates.CheckAsync();
        if (!string.IsNullOrWhiteSpace(_updates.Info.AvailableVersion))
            await _updates.SetLocalizedNotesAsync(_releaseNotes, _localization.CurrentLanguage);
    }

    private void OnCopyDiagnostics(object sender, RoutedEventArgs e)
    {
        try
        {
            var package = new DataPackage();
            package.SetText(DiagnosticsInfoService.Create());
            Clipboard.SetContent(package);
            AppNotificationService.Default.Notify(new(Models.Interaction.AppNotificationKind.Success, T("About_UpdateTitle.Text"), T("Diagnostics_Copied")));
        }
        catch (Exception exception)
        {
            AppLogger.Default.Error("About", "CopyDiagnosticsFailed", exception, "Copying diagnostics failed.");
        }
    }

    private async void OnOpenLogs(object sender, RoutedEventArgs e)
    {
        try
        {
            AppDataPathProvider.Default.EnsureInfrastructureDirectories();
            if (!await Launcher.LaunchFolderPathAsync(AppDataPathProvider.Default.Paths.LogsDirectory)) throw new InvalidOperationException();
        }
        catch (Exception exception)
        {
            AppLogger.Default.Error("About", "OpenLogsFailed", exception, "Opening the log folder failed.");
            AppNotificationService.Default.Notify(new(Models.Interaction.AppNotificationKind.Error, T("Dialog_OpenFailedTitle"), T("Error_OpenLogsFolderFailed")));
        }
    }

    private void RenderUpdate()
    {
        var info = _updates.Info;
        var checking = info.State == AppUpdateState.Checking;
        var display = info.State is AppUpdateState.NotChecked or AppUpdateState.Checking && info.LocalizedReleaseNotes is null && string.IsNullOrWhiteSpace(info.ReleaseNotes)
            ? new ReleaseNotesDisplay(string.Empty, ReleaseNotesDisplaySource.LocalizedEmptyFallback)
            : ReleaseNotesPresentation.Resolve(info, _localization.CurrentLanguage, T("Update_ReleaseNotesUnavailable"));
        AppLogger.Default.Info("ReleaseNotes", "RenderUpdateNotesSource", $"State={info.State}; AvailableVersion={info.AvailableVersion}; Locale={LocalizedReleaseNotesService.NormalizeLocale(_localization.CurrentLanguage)}; Source={display.Source}");

        UpdateVersionText.Text = AppVersionProvider.DisplayVersion;
        var hasTrustedTargetVersion = !string.IsNullOrWhiteSpace(info.AvailableVersion);

        UpdateTargetLabel.Visibility = Visibility.Visible;
        UpdateTargetText.Visibility = Visibility.Visible;
        UpdateTargetText.Text = hasTrustedTargetVersion ? $"v{info.AvailableVersion}" : "—";
        UpdateNotesLabel.Visibility = Visibility.Visible;
        UpdateNotesContainer.Visibility = Visibility.Visible;
        UpdateNotesText.Visibility = Visibility.Visible;
        UpdateNotesText.Text = hasTrustedTargetVersion ? display.Text : T("Update_ReleaseNotesUnavailable");
        UpdateNotesVersionText.Text = hasTrustedTargetVersion ? $"v{info.AvailableVersion}" : "—";

        CheckUpdateButton.IsEnabled = _channel.CanCheckForUpdates && (_updates.CanCheck || info.IsUpdateAvailable);
        CheckUpdateButtonText.Text = checking
            ? T("Update_State_Checking")
            : info.NeedsFinalRestart
                ? T("Action_RestartAndUpdate")
                : info.IsUpdateAvailable
                    ? T("Action_DownloadAndInstall")
                    : T("Action_CheckForUpdates");
        CheckUpdateButtonProgressRing.Visibility = checking ? Visibility.Visible : Visibility.Collapsed;
        CheckUpdateButtonProgressRing.IsActive = checking;

        UpdateStatusText.Text = T($"Update_State_{info.State}");
        var progressVisible = info.State is AppUpdateState.Downloading or AppUpdateState.Verifying or AppUpdateState.Installing or AppUpdateState.Restarting;
        UpdateProgressBar.Visibility = progressVisible ? Visibility.Visible : Visibility.Collapsed;
        UpdateProgressBar.IsIndeterminate = info.State is AppUpdateState.Verifying or AppUpdateState.Installing or AppUpdateState.Restarting || _updates.Progress is null;
        if (_updates.Progress is double progress) UpdateProgressBar.Value = progress * 100d;
        if (info.State == AppUpdateState.Failed)
            UpdateStatusText.Text = $"{T(AppUpdateErrorMapper.ToResourceKey(info.ErrorCode))}{(string.IsNullOrWhiteSpace(info.ErrorCode) ? string.Empty : $" ({info.ErrorCode})")}";
    }

    private async Task OpenLinkAsync(Uri uri)
    {
        if (!await ExternalLinkService.OpenAsync(uri.ToString()))
            AppNotificationService.Default.Notify(new(Models.Interaction.AppNotificationKind.Error, T("Dialog_OpenFailedTitle"), T("Error_OpenExternalLinkFailed")));
    }

    private async Task OpenDocumentAsync(string fileName)
    {
        try
        {
            var file = await StorageFile.GetFileFromApplicationUriAsync(new Uri($"ms-appx:///{fileName}"));
            var text = await FileIO.ReadTextAsync(file);
            await AppDialogService.Default.ShowAsync(new ContentDialog
            {
                XamlRoot = XamlRoot,
                Title = fileName,
                Content = new ScrollViewer { Content = new TextBlock { Text = text, TextWrapping = TextWrapping.Wrap } },
                CloseButtonText = T("Dialog_Ok")
            }, _pageLifetime.Token);
        }
        catch (Exception)
        {
            AppNotificationService.Default.Notify(new(Models.Interaction.AppNotificationKind.Error, T("Dialog_OpenFailedTitle"), T("Error_OpenDocumentFailed")));
        }
    }

    private string L(string zh, string ja, string en)
    {
        var language = _localization.CurrentLanguage;
        if (language.StartsWith("ja", StringComparison.OrdinalIgnoreCase)) return ja;
        if (language.StartsWith("en", StringComparison.OrdinalIgnoreCase)) return en;
        return zh;
    }
    private string T(string key) => _localization.GetString(key);
    private string TFormatted(string key, params object[] arguments) => _localization.GetFormattedString(key, arguments);
    private string ChannelLabel(DistributionChannelContext channel) => T(channel.DisplayResourceKey);
}
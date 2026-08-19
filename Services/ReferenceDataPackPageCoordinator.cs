using System.Collections.Concurrent;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Windows.Storage.Pickers;
using UrbanPlanToolbox.Models;

namespace UrbanPlanToolbox.Services;

public static class ReferenceDataPackPageCoordinator
{
    private static readonly ConcurrentDictionary<string, string> CloudVersionCache = new(StringComparer.Ordinal);

    public static async Task<bool> CheckAndInstallUpdateAsync(FrameworkElement host, string packId, InfoBar statusBar, CancellationToken cancellationToken = default)
    {
        var service = ReferenceDataPackService.Default;
        try
        {
            var update = await service.CheckForUpdateAsync(packId, cancellationToken);
            if (update.Remote is null)
            {
                CloudVersionCache[packId] = ReferenceLibraryText.Get("CloudUnavailable");
                Show(statusBar, GetCatalogFailureText(update.Status), GetCatalogFailureSeverity(update.Status));
                return false;
            }

            CloudVersionCache[packId] = ReferenceLibraryText.Get(
                "CloudVersion",
                update.Remote.Version,
                update.UpdateAvailable ? ReferenceLibraryText.Get("UpdateAvailable", update.Remote.Version) : ReferenceLibraryText.Get("Latest"));

            if (!update.UpdateAvailable)
            {
                Show(statusBar, ReferenceLibraryText.Get("AlreadyLatest"), InfoBarSeverity.Success);
                return false;
            }

            var dialog = new ContentDialog
            {
                XamlRoot = host.XamlRoot,
                Title = ReferenceLibraryText.Get("UpdateAvailable", update.Remote.Version),
                Content = $"{update.Remote.PackId}\n{update.Remote.Version} · Schema {update.Remote.SchemaVersion}",
                PrimaryButtonText = ReferenceLibraryText.Get("DownloadInstall"),
                CloseButtonText = ReferenceLibraryText.Get("Close"),
                DefaultButton = ContentDialogButton.Primary
            };
            if (await AppDialogService.Default.ShowAsync(dialog) != ContentDialogResult.Primary) return false;
            if (string.IsNullOrWhiteSpace(update.Remote.DownloadUrl))
            {
                Show(statusBar, ReferenceLibraryText.Get("CatalogUnavailable"), InfoBarSeverity.Warning);
                return false;
            }

            var installed = await service.DownloadAndInstallAsync(packId, update.Remote, cancellationToken);
            CloudVersionCache[packId] = ReferenceLibraryText.Get("CloudVersion", update.Remote.Version, ReferenceLibraryText.Get("Latest"));
            Show(statusBar, ReferenceLibraryText.Get("UpdateInstalled", installed.Version), InfoBarSeverity.Success);
            return true;
        }
        catch (Exception exception)
        {
            CloudVersionCache[packId] = ReferenceLibraryText.Get("CloudUnavailable");
            AppLogger.Default.Error(nameof(ReferenceDataPackPageCoordinator), "data_update_failed", exception, packId);
            Show(statusBar, ReferenceLibraryText.Get("PackFailed", exception.Message), InfoBarSeverity.Error);
            return false;
        }
    }

    public static async Task<bool> ManageAsync(FrameworkElement host, string packId, InfoBar statusBar, CancellationToken cancellationToken = default)
    {
        var service = ReferenceDataPackService.Default;
        var state = await service.GetActiveStateAsync(packId, cancellationToken);
        var installed = await service.GetInstalledVersionsAsync(packId, cancellationToken);
        var canRollback = state is not null && installed.Any(candidate => ReferenceDataPackService.ParseDataVersion(candidate.Version).CompareTo(ReferenceDataPackService.ParseDataVersion(state.Version)) < 0);
        var source = state?.SourceKind switch
        {
            "official" => ReferenceLibraryText.Get("SourceOfficial"),
            "rollback" => ReferenceLibraryText.Get("SourceRollback"),
            "installed" => ReferenceLibraryText.Get("SourceInstalled"),
            _ => ReferenceLibraryText.Get("SourceLocal")
        };
        var current = state?.Version ?? ReferenceLibraryText.Get("NoDataPack");
        var dialog = new ContentDialog
        {
            XamlRoot = host.XamlRoot,
            Title = ReferenceLibraryText.Get("SourceManagerTitle"),
            Content = ReferenceLibraryText.Get("SourceManagerBody", current, source),
            PrimaryButtonText = ReferenceLibraryText.Get("ImportPack"),
            SecondaryButtonText = canRollback ? ReferenceLibraryText.Get("Rollback") : string.Empty,
            CloseButtonText = ReferenceLibraryText.Get("Close"),
            DefaultButton = ContentDialogButton.Primary
        };
        var result = await AppDialogService.Default.ShowAsync(dialog);
        if (result == ContentDialogResult.Secondary)
        {
            if (await service.RollbackAsync(packId, cancellationToken))
            {
                Show(statusBar, ReferenceLibraryText.Get("RollbackSucceeded"), InfoBarSeverity.Success);
                return true;
            }
            Show(statusBar, ReferenceLibraryText.Get("NoRollback"), InfoBarSeverity.Informational);
            return false;
        }
        if (result != ContentDialogResult.Primary) return false;
        return await ImportFromPickerAsync(host, packId, statusBar, cancellationToken);
    }

    public static async Task<bool> ImportFromPickerAsync(FrameworkElement host, string packId, InfoBar statusBar, CancellationToken cancellationToken = default)
    {
        try
        {
            var picker = new FileOpenPicker();
            picker.FileTypeFilter.Add(".uptdata");
            WinRT.Interop.InitializeWithWindow.Initialize(picker, WinRT.Interop.WindowNative.GetWindowHandle(App.MainWindow));
            var file = await picker.PickSingleFileAsync();
            if (file is null) return false;
            var installed = await ReferenceDataPackService.Default.ImportAsync(packId, file.Path, "local", cancellationToken);
            Show(statusBar, ReferenceLibraryText.Get("ImportSucceeded", installed.Version), InfoBarSeverity.Success);
            return true;
        }
        catch (Exception exception)
        {
            AppLogger.Default.Error(nameof(ReferenceDataPackPageCoordinator), "data_import_failed", exception, packId);
            Show(statusBar, ReferenceLibraryText.Get("PackFailed", exception.Message), InfoBarSeverity.Error);
            return false;
        }
    }

    public static Task<string> GetCloudVersionTextAsync(string packId, CancellationToken cancellationToken = default)
    {
        ReferenceDataPackService.ValidatePackId(packId);
        cancellationToken.ThrowIfCancellationRequested();
        return Task.FromResult(CloudVersionCache.TryGetValue(packId, out var text)
            ? text
            : ReferenceLibraryText.Get("CloudNotChecked"));
    }

    private static string GetCatalogFailureText(string status) => status switch
    {
        "catalog-network-unavailable" or "catalog-timeout" => ReferenceLibraryText.Get("CatalogNetworkUnavailable"),
        "catalog-invalid" => ReferenceLibraryText.Get("CatalogInvalid"),
        "catalog-missing-pack" => ReferenceLibraryText.Get("CatalogMissingPack"),
        _ => ReferenceLibraryText.Get("CatalogUnavailable")
    };

    private static InfoBarSeverity GetCatalogFailureSeverity(string status) => status switch
    {
        "catalog-invalid" or "catalog-missing-pack" => InfoBarSeverity.Warning,
        _ => InfoBarSeverity.Informational
    };

    private static void Show(InfoBar bar, string message, InfoBarSeverity severity)
    {
        bar.Message = message;
        bar.Severity = severity;
        bar.IsOpen = true;
    }
}

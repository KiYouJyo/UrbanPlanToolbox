using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Navigation;
using UrbanPlanToolbox.Services;
using Windows.Storage;

namespace UrbanPlanToolbox.Views;

public sealed partial class AboutPage
{
    protected override void OnNavigatedTo(NavigationEventArgs e)
    {
        base.OnNavigatedTo(e);

        PrivacyButton.Click -= OnOpenPrivacy;
        PrivacyButton.Click -= OnOpenPrivacyFormatted;
        PrivacyButton.Click += OnOpenPrivacyFormatted;

        NoticesButton.Click -= OnOpenNotices;
        NoticesButton.Click -= OnOpenNoticesFormatted;
        NoticesButton.Click += OnOpenNoticesFormatted;
    }

    private async void OnOpenPrivacyFormatted(object sender, RoutedEventArgs e) =>
        await OpenFormattedDocumentAsync(
            "PRIVACY.md",
            L("隐私政策", "プライバシーポリシー", "Privacy policy"));

    private async void OnOpenNoticesFormatted(object sender, RoutedEventArgs e) =>
        await OpenFormattedDocumentAsync(
            "THIRD-PARTY-NOTICES.md",
            L("第三方声明", "第三者声明", "Third-party notices"));

    private async Task OpenFormattedDocumentAsync(string fileName, string title)
    {
        try
        {
            var file = await StorageFile.GetFileFromApplicationUriAsync(new Uri($"ms-appx:///{fileName}"));
            var markdown = await FileIO.ReadTextAsync(file);
            await AppDialogService.Default.ShowAsync(new ContentDialog
            {
                XamlRoot = XamlRoot,
                Title = title,
                Content = MarkdownDocumentView.Build(markdown),
                CloseButtonText = T("Dialog_Ok")
            }, _pageLifetime.Token);
        }
        catch (Exception)
        {
            AppNotificationService.Default.Notify(new(Models.Interaction.AppNotificationKind.Error, T("Dialog_OpenFailedTitle"), T("Error_OpenDocumentFailed")));
        }
    }
}

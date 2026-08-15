using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using UrbanPlanToolbox.Models;
using UrbanPlanToolbox.Services;

namespace UrbanPlanToolbox.Controls;
public sealed partial class InspirationCardsView : UserControl
{
    private InspirationCategory _category;
    public InspirationCardsView() { InitializeComponent(); Loaded += async (_, _) => await RefreshAsync(); }
    public void Configure(InspirationCategory category) { _category = category; Heading.Text = LocalizationService.Default.GetString(category == InspirationCategory.Design ? "Inspiration_DesignHeading" : "Inspiration_ResearchHeading"); }
    public async Task RefreshAsync() => Cards.ItemsSource = (await InspirationService.Default.ListAsync()).Where(item => item.Category == _category).OrderByDescending(item => item.UpdatedAt).Select(item => new Card(item)).ToArray();
    private async void OnCardClick(object sender, RoutedEventArgs e) { if ((sender as FrameworkElement)?.DataContext is Card card) await App.ShowInspirationAsync(card.Item.Id); }
    private sealed record Card(Inspiration Item) { public string Title => Item.Title; public string CategoryText => $"{(Item.Category == InspirationCategory.Design ? LocalizationService.Default.GetString("Inspiration_DesignHeading") : LocalizationService.Default.GetString("Inspiration_ResearchHeading"))} · {LocalizationService.Default.GetString("Inspiration_Unlinked")}"; public string Preview => Item.Content; public string Updated => Item.UpdatedAt.LocalDateTime.ToString("g"); }
}

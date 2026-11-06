using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using UrbanPlanToolbox.Services;
using UrbanPlanToolbox.Models;
namespace UrbanPlanToolbox.Views;

public sealed partial class DatabasePage : Page
{
    public DatabasePage()
    {
        InitializeComponent();
        var l = LocalizationService.Default;
        TitleText.Text = l.GetString("Navigation_Database");
        DescriptionText.Text = l.GetString("Database_Description");
        EmptyText.Text = l.GetString("Database_Empty");
        foreach (var region in new[] { "Asia", "Europe", "Americas", "Africa", "Oceania" })
            Regions.Items.Add(new ListViewItem { Content = l.GetString("Database_" + region), Tag = region });
        Regions.SelectedIndex = 0;
    }
    private void OnRegionChanged(object sender, SelectionChangedEventArgs e)
    {
        var l = LocalizationService.Default;
        var region = (Regions.SelectedItem as ListViewItem)?.Tag as string;
        var countries = CountryDatabaseCatalog.All.Where(c => c.Region == region).Select(c => new CountryCard(c.Code, l.GetString(c.NameKey), l.GetString(c.DescriptionKey))).ToArray();
        Countries.ItemsSource = countries;
        EmptyText.Visibility = countries.Length > 0 ? Visibility.Collapsed : Visibility.Visible;
    }
    private void OnCountryClick(object sender, RoutedEventArgs e)
    {
        if ((sender as Button)?.Tag as string == "JP") Frame.Navigate(typeof(CountryDatabasePage), "JP");
    }
    public sealed record CountryCard(string Code, string Name, string Description);
}

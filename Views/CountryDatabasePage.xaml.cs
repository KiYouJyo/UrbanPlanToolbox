using System.Text;
using System.Text.Json;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.Web.WebView2.Core;
using UrbanPlanToolbox.Services;
using Windows.Storage.Pickers;

namespace UrbanPlanToolbox.Views;

public sealed partial class CountryDatabasePage : Page
{
    private readonly string _dataDirectory = Path.Combine(AppDataPathProvider.Default.Paths.DataDirectory, "databases", "JP");
    private readonly SemaphoreSlim _operations = new(1, 1);
    private bool _closed;
    public CountryDatabasePage()
    {
        InitializeComponent(); Loaded += OnLoaded; Unloaded += OnUnloaded;
    }
    private async void OnLoaded(object sender, RoutedEventArgs e)
    {
        try
        {
            Directory.CreateDirectory(_dataDirectory);
            await Browser.EnsureCoreWebView2Async();
            Browser.CoreWebView2.SetVirtualHostNameToFolderMapping("database.local", Path.Combine(AppContext.BaseDirectory, "Assets", "Database", "Japan"), CoreWebView2HostResourceAccessKind.DenyCors);
            Browser.CoreWebView2.Settings.AreDevToolsEnabled = false;
            Browser.CoreWebView2.Settings.AreDefaultContextMenusEnabled = false;
            Browser.CoreWebView2.NavigationStarting += OnNavigationStarting;
            Browser.CoreWebView2.WebMessageReceived += OnMessage;
            Browser.Source = new Uri("https://database.local/index.html?lang=" + Uri.EscapeDataString(LocalizationService.Default.CurrentLanguage));
        }
        catch (Exception ex) { ShowError(ex); }
    }
    private void OnNavigationStarting(object? sender, CoreWebView2NavigationStartingEventArgs e)
    {
        if (!Uri.TryCreate(e.Uri, UriKind.Absolute, out var uri) || uri.Scheme != "https" || uri.Host != "database.local") e.Cancel = true;
    }
    private void OnUnloaded(object sender, RoutedEventArgs e)
    {
        _closed = true;
        if (Browser.CoreWebView2 is { } core) { core.WebMessageReceived -= OnMessage; core.NavigationStarting -= OnNavigationStarting; }
        Browser.Close();
    }
    private async void OnMessage(object? sender, CoreWebView2WebMessageReceivedEventArgs e)
    {
        if (!Uri.TryCreate(e.Source, UriKind.Absolute, out var uri) || uri.Host != "database.local" || uri.Scheme != "https") return;
        await _operations.WaitAsync();
        try
        {
            using var doc = JsonDocument.Parse(e.WebMessageAsJson);
            var root = doc.RootElement;
            switch (root.GetProperty("type").GetString())
            {
                case "load":
                    var path = Path.Combine(_dataDirectory, "datasets.json");
                    using (var saved = JsonDocument.Parse(File.Exists(path) ? await File.ReadAllTextAsync(path) : "{\"formatVersion\":1,\"country\":\"JP\",\"datasets\":[]}"))
                        Reply(new { type = "loaded", datasets = saved.RootElement.GetProperty("datasets").Clone() });
                    break;
                case "save":
                    var json = root.GetProperty("datasets").GetRawText();
                    if (Encoding.UTF8.GetByteCount(json) > 50 * 1024 * 1024) throw new InvalidDataException("Dataset exceeds 50 MB.");
                    var temporary = Path.Combine(_dataDirectory, "datasets-" + Guid.NewGuid().ToString("N") + ".tmp");
                    try { await File.WriteAllTextAsync(temporary, "{\"formatVersion\":1,\"country\":\"JP\",\"datasets\":" + json + "}"); File.Move(temporary, Path.Combine(_dataDirectory, "datasets.json"), true); }
                    finally { if (File.Exists(temporary)) File.Delete(temporary); }
                    Reply(new { type = "saved" });
                    break;
                case "backup":
                    var backupPicker = new FileSavePicker { SuggestedFileName = "Japan_database_" + DateTime.Now.ToString("yyyyMMdd_HHmmss") };
                    backupPicker.FileTypeChoices.Add("Database JSON", new[] { ".json" });
                    WinRT.Interop.InitializeWithWindow.Initialize(backupPicker, WinRT.Interop.WindowNative.GetWindowHandle(App.MainWindow));
                    var backupFile = await backupPicker.PickSaveFileAsync();
                    if (backupFile is null) { Reply(new { type = "cancelled" }); break; }
                    var backupJson = JsonSerializer.Serialize(new { formatVersion = 1, country = "JP", datasets = root.GetProperty("datasets").Clone() });
                    await File.WriteAllTextAsync(backupFile.Path, backupJson, new UTF8Encoding(false));
                    Reply(new { type = "exported" });
                    break;
                case "export":
                    var format = root.GetProperty("format").GetString() ?? "geojson";
                    if (format is not ("geojson" or "csv" or "shp")) throw new InvalidDataException("Unknown format.");
                    var picker = new FileSavePicker { SuggestedFileName = "Japan_selection_" + DateTime.Now.ToString("yyyyMMdd_HHmmss") };
                    picker.FileTypeChoices.Add(format == "shp" ? "Shapefile ZIP" : format.ToUpperInvariant(), new[] { format == "shp" ? ".zip" : "." + format });
                    WinRT.Interop.InitializeWithWindow.Initialize(picker, WinRT.Interop.WindowNative.GetWindowHandle(App.MainWindow));
                    var file = await picker.PickSaveFileAsync();
                    if (file is null) { Reply(new { type = "cancelled" }); break; }
                    var collection = root.GetProperty("collection").GetRawText();
                    await Task.Run(() => AdministrativeExportService.Export(collection, format, file.Path));
                    Reply(new { type = "exported" });
                    break;
            }
        }
        catch (Exception ex) { ShowError(ex); Reply(new { type = "error", message = ex.Message }); }
        finally { _operations.Release(); }
    }
    private void Reply(object payload) { if (!_closed) Browser.CoreWebView2?.PostWebMessageAsJson(JsonSerializer.Serialize(payload)); }
    private void ShowError(Exception ex)
    {
        AppLogger.Default.Error("Database", "DatabaseOperationFailed", ex, ex.GetType().Name);
        StatusBar.Title = LocalizationService.Default.GetString("Database_Error"); StatusBar.Message = ex.Message;
        StatusBar.Severity = InfoBarSeverity.Error; StatusBar.IsOpen = true;
    }
}

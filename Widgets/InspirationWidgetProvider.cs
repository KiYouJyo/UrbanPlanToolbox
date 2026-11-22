using System.Diagnostics;
using System.Globalization;
using System.Text.Json;
using Microsoft.Windows.Widgets.Providers;
using UrbanPlanToolbox.Models;
using UrbanPlanToolbox.Services;

namespace UrbanPlanToolbox.Widgets;

/// <summary>
/// The Windows Widgets Board activates this provider as a COM local server. No WinUI
/// window is constructed in this process; all records use the existing inspiration store.
/// </summary>
internal sealed class InspirationWidgetProvider : IWidgetProvider
{
    public void CreateWidget(WidgetContext widgetContext) => Update(widgetContext);
    public void DeleteWidget(string widgetId, string customState)
    {
        // The widget service owns pin state. Do not delete actual inspiration records.
        if (WidgetManager.GetDefault().GetWidgetInfos().Length == 0)
            InspirationWidgetHost.RequestStop();
    }

    public void Activate(WidgetContext widgetContext) => Update(widgetContext);
    public void Deactivate(string widgetId) { }
    public void OnWidgetContextChanged(WidgetContextChangedArgs args) => Update(args.WidgetContext);

    public void OnActionInvoked(WidgetActionInvokedArgs args)
    {
        if (string.Equals(args.Verb, "open", StringComparison.Ordinal))
        {
            LaunchRecorder();
            return;
        }
        if (!string.Equals(args.Verb, "save", StringComparison.Ordinal)) return;

        try
        {
            using var payload = JsonDocument.Parse(args.Data ?? "{}");
            if (!payload.RootElement.TryGetProperty("title", out var titleNode) ||
                titleNode.ValueKind != JsonValueKind.String) return;
            var title = titleNode.GetString()?.Trim();
            if (string.IsNullOrWhiteSpace(title) || title.Length > 120) return;

            var category = InspirationCategory.Design;
            if (payload.RootElement.TryGetProperty("category", out var categoryNode) &&
                categoryNode.ValueKind == JsonValueKind.String &&
                string.Equals(categoryNode.GetString(), "Research", StringComparison.OrdinalIgnoreCase))
                category = InspirationCategory.Research;

            var now = DateTimeOffset.UtcNow;
            var saved = InspirationService.Default.CreateAsync(new Inspiration
            {
                Id = Guid.NewGuid(),
                Title = title,
                Category = category,
                CreatedAt = now,
                UpdatedAt = now
            }).GetAwaiter().GetResult();
            if (saved) RefreshAll();
        }
        catch (Exception exception) when (exception is JsonException or IOException or UnauthorizedAccessException)
        {
            // Bad input or transient storage errors must not crash the widget host.
            Trace.TraceError("Inspiration widget save: " + exception.GetType().Name);
        }
    }

    private static void LaunchRecorder()
    {
        var path = Environment.ProcessPath;
        if (string.IsNullOrWhiteSpace(path)) return;
        try
        {
            Process.Start(new ProcessStartInfo(path, "--open-inspiration-recorder")
            {
                UseShellExecute = false
            });
        }
        catch (Exception exception) when (exception is System.ComponentModel.Win32Exception or IOException)
        {
            Trace.TraceError("Inspiration widget launch: " + exception.GetType().Name);
        }
    }

    private static void RefreshAll()
    {
        foreach (var widget in WidgetManager.GetDefault().GetWidgetInfos())
            if (widget.WidgetContext.DefinitionId == "InspirationRecorder")
                Update(widget.WidgetContext);
    }

    private static void Update(WidgetContext context)
    {
        if (context.DefinitionId != "InspirationRecorder") return;
        try
        {
            var ideas = InspirationService.Default.ListAsync().GetAwaiter().GetResult()
                .OrderByDescending(item => item.CreatedAt).Take(2).ToArray();
            var language = GetLanguage();
            var labels = language switch
            {
                "ja" => new[] { "インスピレーション", "最近の記録", "記録はまだありません", "新しいアイデア…", "デザイン", "研究", "保存", "詳細を開く" },
                "en" => new[] { "Inspiration Recorder", "Recent ideas", "No ideas saved yet", "Capture an idea…", "Design", "Research", "Save", "Open recorder" },
                _ => new[] { "灵感记录器", "最近记录", "尚无灵感记录", "快速记录灵感…", "设计", "科研", "保存", "打开记录器" }
            };
            var body = new List<object>
            {
                new { type = "TextBlock", text = labels[0], size = "Medium", weight = "Bolder" },
                new { type = "TextBlock", text = labels[1], isSubtle = true, spacing = "Small" }
            };
            if (ideas.Length == 0)
                body.Add(new { type = "TextBlock", text = labels[2], wrap = true });
            foreach (var item in ideas)
            {
                var prefix = item.Category == InspirationCategory.Research ? labels[5] : labels[4];
                body.Add(new { type = "TextBlock", text = prefix + " · " + item.Title, wrap = true, maxLines = 2 });
            }
            body.Add(new { type = "Input.Text", id = "title", placeholder = labels[3], maxLength = 120, isMultiline = false });
            body.Add(new
            {
                type = "Input.ChoiceSet", id = "category", style = "compact", value = "Design",
                choices = new[]
                {
                    new { title = labels[4], value = "Design" },
                    new { title = labels[5], value = "Research" }
                }
            });

            var actions = new[]
            {
                new { type = "Action.Execute", title = labels[6], verb = "save" },
                new { type = "Action.Execute", title = labels[7], verb = "open" }
            };
            var card = JsonSerializer.Serialize(new
            {
                type = "AdaptiveCard",
                version = "1.6",
                body,
                actions
            });
            WidgetManager.GetDefault().UpdateWidget(new WidgetUpdateRequestOptions(context.Id)
            {
                Template = card,
                Data = "{}",
                IsPlaceholderContent = false
            });
        }
        catch (Exception exception)
        {
            Trace.TraceError("Inspiration widget update: " + exception.GetType().Name);
        }
    }

    private static string GetLanguage()
    {
        var language = CultureInfo.CurrentUICulture.TwoLetterISOLanguageName;
        try
        {
            var settingsPath = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                "UrbanPlanToolbox", "settings.json");
            if (File.Exists(settingsPath))
            {
                using var json = JsonDocument.Parse(File.ReadAllText(settingsPath));
                if (json.RootElement.TryGetProperty("Language", out var node) && node.ValueKind == JsonValueKind.String)
                {
                    var preferred = node.GetString();
                    if (!string.IsNullOrWhiteSpace(preferred) && !string.Equals(preferred, "System", StringComparison.OrdinalIgnoreCase))
                        language = preferred;
                }
            }
        }
        catch (Exception exception) when (exception is IOException or JsonException or UnauthorizedAccessException)
        {
            // System language is a sufficient fallback if user settings are unavailable.
            Trace.TraceInformation("Inspiration widget language fallback: " + exception.GetType().Name);
        }
        return language.StartsWith("ja", StringComparison.OrdinalIgnoreCase) ? "ja" :
            language.StartsWith("en", StringComparison.OrdinalIgnoreCase) ? "en" : "zh";
    }
}

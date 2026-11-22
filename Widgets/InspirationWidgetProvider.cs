using System.Diagnostics;
using System.Globalization;
using System.Text.Json;
using Microsoft.Windows.Widgets.Providers;
using UrbanPlanToolbox.Models;
using UrbanPlanToolbox.Services;

namespace UrbanPlanToolbox.Widgets;

/// <summary>
/// Large-only Adaptive Card reproduction of the original Inspiration Recorder.
/// The host draws the controls; all navigation and persistence stay inside the widget.
/// </summary>
internal sealed class InspirationWidgetProvider : IWidgetProvider
{
    private sealed class EditState
    {
        public Guid? SelectedId { get; set; }
        public bool IsDraft { get; set; }
    }

    public void CreateWidget(WidgetContext widgetContext) => Update(widgetContext, new EditState());

    public void DeleteWidget(string widgetId, string customState)
    {
        // Unpin does not delete saved ideas or drafts.
        if (WidgetManager.GetDefault().GetWidgetInfos().Length == 0)
            InspirationWidgetHost.RequestStop();
    }

    public void Activate(WidgetContext widgetContext) => Update(widgetContext);
    public void Deactivate(string widgetId) { }
    public void OnWidgetContextChanged(WidgetContextChangedArgs args) => Update(args.WidgetContext);

    public void OnActionInvoked(WidgetActionInvokedArgs args)
    {
        var context = args.WidgetContext;
        try
        {
            using var document = JsonDocument.Parse(args.Data ?? "{}");
            var payload = document.RootElement;
            var service = InspirationService.Default;
            var items = service.ListAsync().GetAwaiter().GetResult()
                .OrderBy(idea => idea.CreatedAt).ToArray();
            var state = Normalize(LoadState(context.Id), items);
            var index = GetIndex(state, items);

            switch (args.Verb)
            {
                case "previous":
                case "next":
                {
                    var nextIndex = index + (args.Verb == "previous" ? -1 : 1);
                    if (nextIndex < 0 || nextIndex > items.Length) return;
                    if (!PersistEditor(service, state, payload)) return;
                    Update(context, StateAt(nextIndex, items));
                    return;
                }
                case "new":
                    if (index == items.Length) return;
                    if (!PersistEditor(service, state, payload)) return;
                    // Same behavior as "+" in InspirationRecorderWindow: start a fresh draft.
                    var category = ParseCategory(GetString(payload, "category"));
                    service.SaveDraftAsync(new InspirationDraft { Category = category })
                        .GetAwaiter().GetResult();
                    Update(context, new EditState { IsDraft = true });
                    return;
                case "delete":
                    if (index >= items.Length) return;
                    if (!service.DeleteAsync(items[index].Id).GetAwaiter().GetResult()) return;
                    var remaining = service.ListAsync().GetAwaiter().GetResult()
                        .OrderBy(idea => idea.CreatedAt).ToArray();
                    RefreshAll(StateAt(Math.Min(index, remaining.Length), remaining));
                    return;
                case "save":
                    if (!PersistEditor(service, state, payload)) return;
                    if (state.IsDraft)
                    {
                        var created = service.SaveDraftAsInspirationAsync().GetAwaiter().GetResult();
                        if (created is not null)
                            RefreshAll(new EditState { SelectedId = created.Id });
                    }
                    else
                        RefreshAll(state);
                    return;
                default:
                    return;
            }
        }
        catch (Exception exception) when (exception is JsonException or IOException or UnauthorizedAccessException)
        {
            // Never log user-entered inspiration text.
            Trace.TraceError("Inspiration widget action: " + exception.GetType().Name);
        }
    }

    /// <summary>
    /// Navigation actions gather form inputs as well as changing the selected record.
    /// This mirrors the desktop recorder's persist-on-navigation behavior.
    /// Drafts are stored without creating a saved card until Save is clicked.
    /// </summary>
    private static bool PersistEditor(InspirationService service, EditState state, JsonElement payload)
    {
        var title = GetString(payload, "title")?.Trim();
        var content = GetString(payload, "content")?.Trim();
        if (title is null || content is null || title.Length > 120 || content.Length > 4000)
            return false;

        var category = ParseCategory(GetString(payload, "category"));
        if (state.IsDraft)
        {
            service.SaveDraftAsync(new InspirationDraft
            {
                Title = title, Content = content, Category = category
            }).GetAwaiter().GetResult();
            return true;
        }

        if (state.SelectedId is not Guid id || string.IsNullOrWhiteSpace(title))
            return false;
        var item = service.ListAsync().GetAwaiter().GetResult().FirstOrDefault(x => x.Id == id);
        if (item is null) return false;
        if (item.Title == title && item.Content == content && item.Category == category)
            return true;

        item.Title = title;
        item.Content = content;
        item.Category = category;
        return service.SaveAsync(item).GetAwaiter().GetResult();
    }

    private static InspirationCategory ParseCategory(string? value) =>
        string.Equals(value, "Research", StringComparison.OrdinalIgnoreCase)
            ? InspirationCategory.Research : InspirationCategory.Design;

    private static string? GetString(JsonElement payload, string name) =>
        payload.ValueKind == JsonValueKind.Object &&
        payload.TryGetProperty(name, out var node) &&
        node.ValueKind == JsonValueKind.String ? node.GetString() : null;

    private static EditState LoadState(string widgetId)
    {
        try
        {
            var state = WidgetManager.GetDefault().GetWidgetInfo(widgetId).CustomState;
            if (!string.IsNullOrWhiteSpace(state))
                return JsonSerializer.Deserialize<EditState>(state) ?? new EditState();
        }
        catch (Exception exception) when (exception is JsonException or ArgumentException)
        {
            Trace.TraceInformation("Inspiration widget state fallback: " + exception.GetType().Name);
        }
        return new EditState();
    }

    private static EditState Normalize(EditState state, Inspiration[] items)
    {
        if (state.IsDraft || items.Length == 0)
            return new EditState { IsDraft = true };
        if (state.SelectedId is Guid id && items.Any(x => x.Id == id))
            return new EditState { SelectedId = id };
        return new EditState { SelectedId = items[0].Id };
    }

    private static int GetIndex(EditState state, Inspiration[] items)
    {
        if (state.IsDraft) return items.Length;
        var index = Array.FindIndex(items, x => x.Id == state.SelectedId);
        return index >= 0 ? index : items.Length;
    }

    private static EditState StateAt(int index, Inspiration[] items) =>
        index >= items.Length
            ? new EditState { IsDraft = true }
            : new EditState { SelectedId = items[index].Id };

    private static void RefreshAll(EditState state)
    {
        foreach (var widget in WidgetManager.GetDefault().GetWidgetInfos())
            if (widget.WidgetContext.DefinitionId == "InspirationRecorder")
                Update(widget.WidgetContext, state);
    }

    private static void Update(WidgetContext context, EditState? requestedState = null)
    {
        if (context.DefinitionId != "InspirationRecorder") return;
        try
        {
            var service = InspirationService.Default;
            var ideas = service.ListAsync().GetAwaiter().GetResult()
                .OrderBy(idea => idea.CreatedAt).ToArray();
            var state = Normalize(requestedState ?? LoadState(context.Id), ideas);
            var index = GetIndex(state, ideas);
            var selected = index < ideas.Length ? ideas[index] : null;
            var draft = state.IsDraft ? service.GetDraftAsync().GetAwaiter().GetResult() : null;

            var labels = GetLanguage() switch
            {
                "ja" => new[] { "名称", "分類", "詳細情報", "デザイン", "研究", "削除", "保存", "新規", "未保存" },
                "en" => new[] { "Name", "Category", "Details", "Design", "Research", "Delete", "Save", "New", "Unsaved" },
                _ => new[] { "名称", "分类", "详细信息", "设计", "科研", "删除", "保存", "新建", "未保存" }
            };

            var currentCategory = (selected?.Category ?? draft?.Category ?? InspirationCategory.Design).ToString();
            var status = state.IsDraft
                ? labels[7] + ((draft?.IsDirty ?? false) ? " · " + labels[8] : "")
                : (index + 1).ToString(CultureInfo.InvariantCulture) +
                  " / " + ideas.Length.ToString(CultureInfo.InvariantCulture);

            var body = new List<object>
            {
                // The Widgets Board already supplies the widget's app/title chrome.
                // A single position line avoids the previous repeated headings.
                new { type = "TextBlock", text = status, size = "Medium", weight = "Bolder" },

                // Mirrors InspirationRecorderWindow.xaml's 3:1 Name/Category header row.
                new
                {
                    type = "ColumnSet",
                    spacing = "Medium",
                    columns = new object[]
                    {
                        new
                        {
                            type = "Column", width = "3",
                            items = new object[]
                            {
                                new
                                {
                                    type = "Input.Text", id = "title", label = labels[0],
                                    value = selected?.Title ?? draft?.Title ?? "",
                                    placeholder = labels[0], maxLength = 120
                                }
                            }
                        },
                        new
                        {
                            type = "Column", width = "1",
                            items = new object[]
                            {
                                new
                                {
                                    type = "Input.ChoiceSet", id = "category",
                                    label = labels[1], style = "compact",
                                    value = currentCategory,
                                    choices = new[]
                                    {
                                        new { title = labels[3], value = "Design" },
                                        new { title = labels[4], value = "Research" }
                                    }
                                }
                            }
                        }
                    }
                },
                new
                {
                    type = "Input.Text", id = "content", label = labels[2],
                    value = selected?.Content ?? draft?.Content ?? "",
                    placeholder = labels[2], isMultiline = true, maxLength = 4000
                },

                // The host renders controls, but the two button groups preserve the
                // recorder's bottom-left ‹ › + and bottom-right Delete Save layout.
                new
                {
                    type = "ColumnSet",
                    spacing = "Small",
                    columns = new object[]
                    {
                        new
                        {
                            type = "Column", width = "stretch",
                            items = new object[]
                            {
                                new
                                {
                                    type = "ActionSet",
                                    actions = new object[]
                                    {
                                        new { type = "Action.Execute", title = "‹", verb = "previous",
                                            isEnabled = index > 0 },
                                        new { type = "Action.Execute", title = "›", verb = "next",
                                            isEnabled = index < ideas.Length },
                                        new { type = "Action.Execute", title = "+", verb = "new",
                                            isEnabled = index < ideas.Length }
                                    }
                                }
                            }
                        },
                        new
                        {
                            type = "Column", width = "auto",
                            items = new object[]
                            {
                                new
                                {
                                    type = "ActionSet",
                                    actions = new object[]
                                    {
                                        new { type = "Action.Execute", title = labels[5], verb = "delete",
                                            associatedInputs = "none", isEnabled = index < ideas.Length },
                                        new { type = "Action.Execute", title = labels[6], verb = "save" }
                                    }
                                }
                            }
                        }
                    }
                }
            };

            var card = JsonSerializer.Serialize(new
            {
                type = "AdaptiveCard",
                version = "1.6",
                body
            });
            WidgetManager.GetDefault().UpdateWidget(new WidgetUpdateRequestOptions(context.Id)
            {
                Template = card,
                Data = "{}",
                CustomState = JsonSerializer.Serialize(state),
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

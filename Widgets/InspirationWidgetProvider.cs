using System.Diagnostics;
using System.Globalization;
using System.Text.Json;
using Microsoft.Windows.Widgets.Providers;
using UrbanPlanToolbox.Models;
using UrbanPlanToolbox.Services;

namespace UrbanPlanToolbox.Widgets;

/// <summary>
/// Self-contained Inspiration Recorder within Windows 11 Widgets Board.
/// Never opens or depends on the desktop recorder window.
/// </summary>
internal sealed class InspirationWidgetProvider : IWidgetProvider
{
    private sealed class EditState
    {
        public Guid? SelectedId { get; set; }
        public bool ConfirmDelete { get; set; }
    }

    public void CreateWidget(WidgetContext widgetContext) => Update(widgetContext, new EditState());

    public void DeleteWidget(string widgetId, string customState)
    {
        // Unpinning does not remove user records.
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
            using var json = JsonDocument.Parse(args.Data ?? "{}");
            var payload = json.RootElement;
            var state = LoadState(context.Id);
            var service = InspirationService.Default;
            switch (args.Verb)
            {
                case "new":
                    Update(context, new EditState());
                    return;
                case "load":
                    if (Guid.TryParse(GetString(payload, "historyId"), out var chosenId))
                        Update(context, new EditState { SelectedId = chosenId });
                    return;
                case "cancel-delete":
                    state.ConfirmDelete = false;
                    Update(context, state);
                    return;
                case "delete":
                    if (state.SelectedId.HasValue)
                    {
                        state.ConfirmDelete = true;
                        Update(context, state);
                    }
                    return;
                case "confirm-delete":
                    if (state.SelectedId.HasValue && state.ConfirmDelete)
                    {
                        service.DeleteAsync(state.SelectedId.Value).GetAwaiter().GetResult();
                        RefreshAll(new EditState());
                    }
                    return;
                case "save-draft":
                case "save":
                {
                    var title = GetString(payload, "title")?.Trim() ?? string.Empty;
                    var content = GetString(payload, "content")?.Trim() ?? string.Empty;
                    if (title.Length > 120 || content.Length > 4000) return;
                    var category = string.Equals(GetString(payload, "category"), "Research",
                        StringComparison.OrdinalIgnoreCase) ? InspirationCategory.Research : InspirationCategory.Design;
                    if (args.Verb == "save-draft")
                    {
                        // Keep the existing draft model distinct from saved inspiration cards.
                        service.SaveDraftAsync(new InspirationDraft
                        {
                            Title = title, Content = content, Category = category
                        }).GetAwaiter().GetResult();
                        Update(context, new EditState());
                        return;
                    }
                    if (string.IsNullOrWhiteSpace(title)) return;
                    Guid savedId;
                    if (state.SelectedId is Guid id)
                    {
                        // Preserve project association and creation timestamp when editing.
                        var item = service.ListAsync().GetAwaiter().GetResult().FirstOrDefault(x => x.Id == id);
                        if (item is null) { Update(context, new EditState()); return; }
                        item.Title = title;
                        item.Content = content;
                        item.Category = category;
                        if (!service.SaveAsync(item).GetAwaiter().GetResult()) return;
                        savedId = id;
                    }
                    else
                    {
                        savedId = Guid.NewGuid();
                        var now = DateTimeOffset.UtcNow;
                        if (!service.CreateAsync(new Inspiration
                        {
                            Id = savedId,
                            Title = title,
                            Content = content,
                            Category = category,
                            CreatedAt = now,
                            UpdatedAt = now
                        }).GetAwaiter().GetResult()) return;
                    }
                    RefreshAll(new EditState { SelectedId = savedId });
                    return;
                }
                default:
                    return;
            }
        }
        catch (Exception exception) when (exception is JsonException or IOException or UnauthorizedAccessException)
        {
            // Never log idea text or raw action payloads.
            Trace.TraceError("Inspiration widget action: " + exception.GetType().Name);
        }
    }

    private static string? GetString(JsonElement payload, string name)
    {
        if (payload.ValueKind != JsonValueKind.Object ||
            !payload.TryGetProperty(name, out var element) || element.ValueKind != JsonValueKind.String)
            return null;
        return element.GetString();
    }

    private static EditState LoadState(string widgetId)
    {
        try
        {
            var customState = WidgetManager.GetDefault().GetWidgetInfo(widgetId).CustomState;
            if (!string.IsNullOrWhiteSpace(customState))
                return JsonSerializer.Deserialize<EditState>(customState) ?? new EditState();
        }
        catch (Exception exception) when (exception is JsonException or ArgumentException)
        {
            Trace.TraceInformation("Inspiration widget reset state: " + exception.GetType().Name);
        }
        return new EditState();
    }

    private static void RefreshAll(EditState? selectedState = null)
    {
        foreach (var widget in WidgetManager.GetDefault().GetWidgetInfos())
            if (widget.WidgetContext.DefinitionId == "InspirationRecorder")
                Update(widget.WidgetContext, selectedState);
    }

    private static void Update(WidgetContext context, EditState? requestedState = null)
    {
        if (context.DefinitionId != "InspirationRecorder") return;
        try
        {
            var state = requestedState ?? LoadState(context.Id);
            var service = InspirationService.Default;
            var ideas = service.ListAsync().GetAwaiter().GetResult()
                .OrderByDescending(x => x.CreatedAt).ToArray();
            var selected = state.SelectedId is Guid id
                ? ideas.FirstOrDefault(x => x.Id == id) : null;
            if (state.SelectedId.HasValue && selected is null)
                state = new EditState();

            var draft = selected is null ? service.GetDraftAsync().GetAwaiter().GetResult() : null;
            var language = GetLanguage();
            var labels = language switch
            {
                "ja" => new[] { "インスピレーション", "新しい記録", "既存の記録", "選択して編集", "タイトル", "内容", "デザイン", "研究", "保存", "新規", "削除", "削除しますか？", "削除を確定", "キャンセル", "下書きを保存", "読み込む", "記録なし" },
                "en" => new[] { "Inspiration Recorder", "New idea", "Saved ideas", "Choose an idea to edit", "Title", "Details", "Design", "Research", "Save", "New", "Delete", "Delete this idea?", "Confirm delete", "Cancel", "Save draft", "Load", "No saved ideas" },
                _ => new[] { "灵感记录器", "新灵感", "历史灵感", "选择灵感以编辑", "标题", "正文", "设计", "科研", "保存", "新建", "删除", "确定删除这条灵感？", "确认删除", "取消", "保存草稿", "加载", "暂无历史记录" }
            };

            var body = new List<object>
            {
                new { type = "TextBlock", text = labels[0], size = "Medium", weight = "Bolder" },
                new { type = "TextBlock", text = selected is null ? labels[1] : selected.Title,
                    size = "Small", weight = "Bolder", wrap = true, maxLines = 2 }
            };

            if (ideas.Length > 0)
            {
                body.Add(new { type = "TextBlock", text = labels[2], isSubtle = true, spacing = "Small" });
                body.Add(new
                {
                    type = "Input.ChoiceSet", id = "historyId", style = "compact", placeholder = labels[3],
                    choices = ideas.Take(50).Select(item => new
                    {
                        title = (item.Category == InspirationCategory.Research ? labels[7] : labels[6])
                                + " · " + Truncate(item.Title, 36),
                        value = item.Id.ToString("D")
                    }).ToArray()
                });
                body.Add(new
                {
                    type = "ActionSet",
                    actions = new[] { new { type = "Action.Execute", verb = "load", title = labels[15] } }
                });
            }
            else
            {
                body.Add(new { type = "TextBlock", text = labels[16], isSubtle = true });
            }

            body.Add(new
            {
                type = "Input.Text", id = "title", label = labels[4],
                placeholder = labels[4], value = selected?.Title ?? draft?.Title ?? "",
                maxLength = 120
            });
            body.Add(new
            {
                type = "Input.Text", id = "content", label = labels[5],
                placeholder = labels[5], value = selected?.Content ?? draft?.Content ?? "",
                isMultiline = true, maxLength = 4000
            });
            body.Add(new
            {
                type = "Input.ChoiceSet", id = "category", style = "compact",
                value = (selected?.Category ?? draft?.Category ?? InspirationCategory.Design).ToString(),
                choices = new[]
                {
                    new { title = labels[6], value = "Design" },
                    new { title = labels[7], value = "Research" }
                }
            });

            var actions = new List<object>
            {
                new { type = "Action.Execute", title = labels[8], verb = "save" },
                new { type = "Action.Execute", title = labels[9], verb = "new", associatedInputs = "none" }
            };

            if (selected is not null)
            {
                if (state.ConfirmDelete)
                {
                    body.Add(new { type = "TextBlock", text = labels[11], color = "Attention", wrap = true });
                    actions.Add(new { type = "Action.Execute", title = labels[12], verb = "confirm-delete", associatedInputs = "none" });
                    actions.Add(new { type = "Action.Execute", title = labels[13], verb = "cancel-delete", associatedInputs = "none" });
                }
                else
                    actions.Add(new { type = "Action.Execute", title = labels[10], verb = "delete", associatedInputs = "none" });
            }
            else
            {
                actions.Add(new { type = "Action.Execute", title = labels[14], verb = "save-draft" });
            }

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
                CustomState = JsonSerializer.Serialize(state),
                IsPlaceholderContent = false
            });
        }
        catch (Exception exception)
        {
            Trace.TraceError("Inspiration widget update: " + exception.GetType().Name);
        }
    }

    private static string Truncate(string text, int maxLength) =>
        text.Length > maxLength ? text[..(maxLength - 1)] + "…" : text;

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

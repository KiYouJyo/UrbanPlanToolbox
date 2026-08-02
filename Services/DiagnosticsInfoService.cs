using System.Runtime.InteropServices;
using System.Text.RegularExpressions;

namespace UrbanPlanToolbox.Services;

public static class DiagnosticsInfoService
{
    public static string Create(string? errorSummary = null)
    {
        var settings = new SettingsService().Load();
        var sdk = AppDomain.CurrentDomain.GetAssemblies()
            .FirstOrDefault(assembly => assembly.GetName().Name?.Contains("WindowsApp", StringComparison.OrdinalIgnoreCase) == true)
            ?.GetName().Version?.ToString() ?? "未记录";
        return string.Join(Environment.NewLine,
            $"应用名称: UrbanPlanToolbox",
            $"应用版本: {AppVersionProvider.DisplayVersion}",
            $"发行渠道: {GetChannelLabel(DistributionChannelProvider.Current)}",
            $"系统架构: {RuntimeInformation.OSArchitecture}",
            $"Windows 版本: {Environment.OSVersion.Version}",
            $"Windows App SDK 版本: {sdk}",
            $"当前界面语言: {LanguagePreference.Normalize(settings.Language)}",
            $"数据架构版本: {AppVersionProvider.DataSchemaVersion}",
            "通知功能: 本地计划提醒（无自动上传）",
            $"错误摘要: {Sanitize(errorSummary) ?? "无"}");
    }

    public static string GetChannelLabel(DistributionChannel channel) => channel == DistributionChannel.Store ? "Microsoft Store" : "GitHub 侧载";

    private static string? Sanitize(string? value)
    {
        if (string.IsNullOrWhiteSpace(value)) return null;
        var text = value.Replace(Environment.NewLine, " ").Replace('\r', ' ').Replace('\n', ' ');
        text = Regex.Replace(text, @"[A-Za-z]:\\[^\s]+", "[path]", RegexOptions.CultureInvariant);
        return text.Length <= 240 ? text : text[..240];
    }
}

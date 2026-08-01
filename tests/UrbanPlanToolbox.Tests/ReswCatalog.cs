using System.Xml;

namespace UrbanPlanToolbox.Tests;

/// <summary>
/// Loads the real RESW files from the test output (copied from the app's
/// Strings directory) so tests validate the actual shipped resources.
/// </summary>
internal static class ReswCatalog
{
    public static IReadOnlyList<string> Languages { get; } = ["zh-CN", "ja-JP", "en-US"];

    public static IReadOnlyDictionary<string, string> Load(string language)
    {
        var path = Path.Combine(AppContext.BaseDirectory, "Strings", language, "Resources.resw");
        var document = new XmlDocument();
        document.Load(path);

        var values = new Dictionary<string, string>(StringComparer.Ordinal);
        foreach (XmlNode node in document.DocumentElement!.ChildNodes)
        {
            if (node.NodeType != XmlNodeType.Element || node.Name != "data")
            {
                continue;
            }

            var name = node.Attributes?["name"]?.Value;
            var value = node["value"]?.InnerText;
            if (name is not null && value is not null)
            {
                values[name] = value;
            }
        }

        return values;
    }
}

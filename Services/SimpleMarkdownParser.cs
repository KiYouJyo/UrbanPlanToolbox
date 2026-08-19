using System.Text;
using System.Text.RegularExpressions;

namespace UrbanPlanToolbox.Services;

public enum SimpleMarkdownBlockKind
{
    Heading1,
    Heading2,
    Heading3,
    Paragraph,
    UnorderedListItem,
    OrderedListItem,
    CodeBlock,
    Separator
}

public sealed record SimpleMarkdownBlock(SimpleMarkdownBlockKind Kind, string Text, string? Prefix = null);

public static partial class SimpleMarkdownParser
{
    public static IReadOnlyList<SimpleMarkdownBlock> Parse(string? markdown)
    {
        if (string.IsNullOrWhiteSpace(markdown)) return [];

        var blocks = new List<SimpleMarkdownBlock>();
        var paragraph = new List<string>();
        var code = new StringBuilder();
        var inCode = false;

        void FlushParagraph()
        {
            if (paragraph.Count == 0) return;
            blocks.Add(new(SimpleMarkdownBlockKind.Paragraph, StripInline(string.Join(" ", paragraph))));
            paragraph.Clear();
        }

        void FlushCode()
        {
            if (code.Length == 0) return;
            blocks.Add(new(SimpleMarkdownBlockKind.CodeBlock, code.ToString().TrimEnd('\r', '\n')));
            code.Clear();
        }

        foreach (var rawLine in markdown.Replace("\r\n", "\n", StringComparison.Ordinal).Replace('\r', '\n').Split('\n'))
        {
            var line = rawLine.TrimEnd();
            var trimmed = line.TrimStart();

            if (trimmed.StartsWith("```", StringComparison.Ordinal))
            {
                FlushParagraph();
                if (inCode) FlushCode();
                inCode = !inCode;
                continue;
            }

            if (inCode)
            {
                code.AppendLine(line);
                continue;
            }

            if (string.IsNullOrWhiteSpace(trimmed))
            {
                FlushParagraph();
                continue;
            }

            if (trimmed is "---" or "***" or "___")
            {
                FlushParagraph();
                blocks.Add(new(SimpleMarkdownBlockKind.Separator, string.Empty));
                continue;
            }

            if (TryHeading(trimmed, out var headingKind, out var heading))
            {
                FlushParagraph();
                blocks.Add(new(headingKind, StripInline(heading)));
                continue;
            }

            if (trimmed.StartsWith("- ", StringComparison.Ordinal) || trimmed.StartsWith("* ", StringComparison.Ordinal) || trimmed.StartsWith("+ ", StringComparison.Ordinal))
            {
                FlushParagraph();
                blocks.Add(new(SimpleMarkdownBlockKind.UnorderedListItem, StripInline(trimmed[2..]), "•"));
                continue;
            }

            var ordered = OrderedListRegex().Match(trimmed);
            if (ordered.Success)
            {
                FlushParagraph();
                blocks.Add(new(SimpleMarkdownBlockKind.OrderedListItem, StripInline(ordered.Groups[2].Value), ordered.Groups[1].Value + "."));
                continue;
            }

            paragraph.Add(trimmed);
        }

        FlushParagraph();
        if (inCode) FlushCode();
        return blocks;
    }

    public static string StripInline(string text)
    {
        if (string.IsNullOrEmpty(text)) return string.Empty;
        var value = ImageRegex().Replace(text, match => match.Groups[1].Value);
        value = LinkRegex().Replace(value, match =>
        {
            var label = match.Groups[1].Value;
            var url = match.Groups[2].Value;
            return string.Equals(label, url, StringComparison.Ordinal) ? url : $"{label} ({url})";
        });
        value = AutoLinkRegex().Replace(value, "$1");
        value = value.Replace("**", string.Empty, StringComparison.Ordinal)
            .Replace("__", string.Empty, StringComparison.Ordinal)
            .Replace("~~", string.Empty, StringComparison.Ordinal)
            .Replace("`", string.Empty, StringComparison.Ordinal);
        return value.Trim();
    }

    private static bool TryHeading(string line, out SimpleMarkdownBlockKind kind, out string text)
    {
        kind = SimpleMarkdownBlockKind.Paragraph;
        text = string.Empty;
        if (line.StartsWith("### ", StringComparison.Ordinal))
        {
            kind = SimpleMarkdownBlockKind.Heading3;
            text = line[4..];
            return true;
        }
        if (line.StartsWith("## ", StringComparison.Ordinal))
        {
            kind = SimpleMarkdownBlockKind.Heading2;
            text = line[3..];
            return true;
        }
        if (line.StartsWith("# ", StringComparison.Ordinal))
        {
            kind = SimpleMarkdownBlockKind.Heading1;
            text = line[2..];
            return true;
        }
        return false;
    }

    [GeneratedRegex(@"^(\d+)[.)]\s+(.+)$", RegexOptions.CultureInvariant)]
    private static partial Regex OrderedListRegex();

    [GeneratedRegex(@"!\[([^\]]*)\]\([^)]*\)", RegexOptions.CultureInvariant)]
    private static partial Regex ImageRegex();

    [GeneratedRegex(@"\[([^\]]+)\]\(([^)]+)\)", RegexOptions.CultureInvariant)]
    private static partial Regex LinkRegex();

    [GeneratedRegex(@"<((?:https?|mailto):[^>]+)>", RegexOptions.CultureInvariant | RegexOptions.IgnoreCase)]
    private static partial Regex AutoLinkRegex();
}

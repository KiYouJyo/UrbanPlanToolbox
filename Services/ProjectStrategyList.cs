namespace UrbanPlanToolbox.Services;

public static class ProjectStrategyList
{
    private static readonly char[] Separators = ['\r', '\n', '；', ';'];

    public static IReadOnlyList<string> Parse(string? source)
    {
        if (string.IsNullOrWhiteSpace(source)) return [];
        return source
            .Split(Separators, StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Where(item => !string.IsNullOrWhiteSpace(item))
            .ToArray();
    }

    public static string Serialize(IEnumerable<string> strategies)
    {
        ArgumentNullException.ThrowIfNull(strategies);
        return string.Join(Environment.NewLine,
            strategies.Select(item => item?.Trim())
                .Where(item => !string.IsNullOrWhiteSpace(item))!);
    }

    public static int Count(string? source) => Parse(source).Count;
}

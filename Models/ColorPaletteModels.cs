namespace UrbanPlanToolbox.Models;

public static class ColorPaletteCategories
{
    public const string Warm = "warm";
    public const string Cool = "cool";
    public const string Neutral = "neutral";
    public const string Monochrome = "monochrome";
    public const string Mixed = "mixed";
    public const string Custom = "custom";

    public static readonly IReadOnlyList<string> All = [Warm, Cool, Neutral, Monochrome, Mixed, Custom];
}

public sealed class ColorPaletteDocument
{
    public List<ColorPaletteScheme> Schemes { get; init; } = [];
}

public sealed class ColorPaletteScheme
{
    public Guid SchemeId { get; init; } = Guid.NewGuid();
    public string Name { get; set; } = "";
    public string Category { get; set; } = ColorPaletteCategories.Neutral;
    public string? CustomCategoryName { get; set; }
    public List<ColorPaletteImage> Images { get; init; } = [];
    public List<ColorPaletteColor> Colors { get; init; } = [];
    public DateTimeOffset CreatedAtUtc { get; init; } = DateTimeOffset.UtcNow;
    public DateTimeOffset UpdatedAtUtc { get; set; } = DateTimeOffset.UtcNow;
}

public sealed class ColorPaletteImage
{
    public Guid ImageId { get; init; } = Guid.NewGuid();
    public string RelativePath { get; init; } = "";
    public string OriginalFileName { get; init; } = "";
    public string ContentType { get; init; } = "";
    public int SortOrder { get; init; }
}

public sealed class ColorPaletteColor
{
    public Guid ColorId { get; init; } = Guid.NewGuid();
    public string? Name { get; set; }
    public string Hex { get; set; } = "#000000";
    public int SortOrder { get; init; }
}

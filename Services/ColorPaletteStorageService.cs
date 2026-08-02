using System.Text.RegularExpressions;
using UrbanPlanToolbox.Models;
using UrbanPlanToolbox.Models.Tools;

namespace UrbanPlanToolbox.Services;

/// <summary>Owns the independent color-palette schema and its managed image copies.</summary>
public sealed class ColorPaletteStorageService
{
    public const int ColorPaletteSchemaVersion = 1;
    public const string DataFileName = "palettes.json";
    private static readonly Regex HexPattern = new("^#[0-9A-F]{6}$", RegexOptions.CultureInvariant);
    private static readonly HashSet<string> SupportedExtensions = new(StringComparer.OrdinalIgnoreCase) { ".png", ".jpg", ".jpeg", ".webp", ".bmp", ".gif", ".tif", ".tiff" };
    private readonly IAppDataPathProvider _paths;
    private readonly JsonDataStorage _storage;

    public ColorPaletteStorageService(IAppDataPathProvider paths, IStorageDiagnostics? diagnostics = null)
    {
        _paths = paths ?? throw new ArgumentNullException(nameof(paths));
        _storage = new JsonDataStorage(paths, ColorPaletteSchemaVersion, diagnostics: diagnostics);
    }

    public async Task<DataReadResult<ColorPaletteDocument>> ReadAsync(CancellationToken cancellationToken = default)
    {
        var result = await _storage.ReadAsync<ColorPaletteDocument>(ToolIds.ColorPaletteRecorder, DataFileName, cancellationToken);
        if (result.Status == DataStorageStatus.NotFound) return new(DataStorageStatus.Success, new ColorPaletteDocument(), ColorPaletteSchemaVersion);
        if (result.HasValue && !TryValidateDocument(result.Value!, out _)) return new(DataStorageStatus.Corrupt, null, result.SchemaVersion, "PaletteInvalid");
        return result;
    }

    public async Task<DataWriteResult> SaveAsync(ColorPaletteDocument document, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(document);
        if (!TryValidateDocument(document, out var error)) return new(DataStorageStatus.IoFailure, error);
        return await _storage.SaveAsync(ToolIds.ColorPaletteRecorder, DataFileName, document, cancellationToken);
    }

    public async Task<ColorPaletteImage> CopyImageAsync(Guid schemeId, string sourcePath, int sortOrder, CancellationToken cancellationToken = default)
    {
        if (schemeId == Guid.Empty || string.IsNullOrWhiteSpace(sourcePath) || !Path.IsPathFullyQualified(sourcePath) || !File.Exists(sourcePath)) throw new ArgumentException("Image source is invalid.", nameof(sourcePath));
        var extension = Path.GetExtension(sourcePath);
        if (!SupportedExtensions.Contains(extension)) throw new NotSupportedException("Unsupported image type.");
        var imageId = Guid.NewGuid();
        var directory = _paths.GetToolAttachmentDirectory(ToolIds.ColorPaletteRecorder, schemeId.ToString("D"));
        var fileName = $"{imageId:N}{extension.ToLowerInvariant()}";
        var destination = Path.Combine(directory, fileName);
        await using var input = File.OpenRead(sourcePath);
        await using var output = File.Create(destination);
        await input.CopyToAsync(output, cancellationToken);
        return new ColorPaletteImage { ImageId = imageId, RelativePath = Path.Combine(schemeId.ToString("D"), fileName).Replace('\\', '/'), OriginalFileName = Path.GetFileName(sourcePath), ContentType = GetContentType(extension), SortOrder = sortOrder };
    }

    public void DeleteManagedImage(ColorPaletteImage image)
    {
        var path = ResolveManagedImagePath(image.RelativePath);
        if (File.Exists(path)) File.Delete(path);
    }

    public void DeleteSchemeAttachments(Guid schemeId)
    {
        if (schemeId == Guid.Empty) throw new ArgumentException("Scheme ID cannot be empty.", nameof(schemeId));
        var root = _paths.GetToolAttachmentsDirectory(ToolIds.ColorPaletteRecorder);
        var path = Path.Combine(root, schemeId.ToString("D"));
        if (Directory.Exists(path)) Directory.Delete(path, true);
    }

    public string ResolveManagedImagePath(string relativePath)
    {
        if (string.IsNullOrWhiteSpace(relativePath) || Path.IsPathRooted(relativePath) || relativePath.Contains("..", StringComparison.Ordinal) || relativePath.Contains('\\')) throw new ArgumentException("Image path is invalid.", nameof(relativePath));
        var root = _paths.GetToolAttachmentsDirectory(ToolIds.ColorPaletteRecorder);
        var path = Path.GetFullPath(Path.Combine(root, relativePath.Replace('/', Path.DirectorySeparatorChar)));
        if (!path.StartsWith(Path.GetFullPath(root) + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase)) throw new ArgumentException("Image path escapes managed storage.", nameof(relativePath));
        return path;
    }

    public static bool TryNormalizeHex(string? value, out string normalized)
    {
        normalized = "";
        if (string.IsNullOrWhiteSpace(value)) return false;
        var candidate = value.Trim().ToUpperInvariant();
        if (!candidate.StartsWith('#')) candidate = "#" + candidate;
        if (!HexPattern.IsMatch(candidate)) return false;
        normalized = candidate;
        return true;
    }

    public static ColorPaletteDocument CloneDocument(ColorPaletteDocument source) => new()
    {
        Schemes = source.Schemes.Select(CloneScheme).ToList()
    };

    public static ColorPaletteScheme CloneScheme(ColorPaletteScheme source) => new()
    {
        SchemeId = source.SchemeId,
        Name = source.Name,
        Category = source.Category,
        CustomCategoryName = source.CustomCategoryName,
        CreatedAtUtc = source.CreatedAtUtc,
        UpdatedAtUtc = source.UpdatedAtUtc,
        Images = source.Images.Select(image => new ColorPaletteImage { ImageId = image.ImageId, RelativePath = image.RelativePath, OriginalFileName = image.OriginalFileName, ContentType = image.ContentType, SortOrder = image.SortOrder }).ToList(),
        Colors = source.Colors.Select(color => new ColorPaletteColor { ColorId = color.ColorId, Name = color.Name, Hex = color.Hex, SortOrder = color.SortOrder }).ToList()
    };

    public static bool TryValidateDocument(ColorPaletteDocument document, out string? error)
    {
        error = null;
        var ids = new HashSet<Guid>();
        foreach (var scheme in document.Schemes)
        {
            if (scheme.SchemeId == Guid.Empty || !ids.Add(scheme.SchemeId) || string.IsNullOrWhiteSpace(scheme.Name) || !ColorPaletteCategories.All.Contains(scheme.Category, StringComparer.Ordinal) || (scheme.Category == ColorPaletteCategories.Custom && string.IsNullOrWhiteSpace(scheme.CustomCategoryName))) { error = "SchemeInvalid"; return false; }
            if (scheme.Images.Select(item => item.ImageId).Distinct().Count() != scheme.Images.Count || scheme.Images.Any(item => item.ImageId == Guid.Empty || string.IsNullOrWhiteSpace(item.RelativePath) || Path.IsPathRooted(item.RelativePath) || item.RelativePath.Contains("..", StringComparison.Ordinal))) { error = "ImageInvalid"; return false; }
            if (scheme.Colors.Select(item => item.ColorId).Distinct().Count() != scheme.Colors.Count) { error = "ColorInvalid"; return false; }
            foreach (var color in scheme.Colors)
            {
                if (color.ColorId == Guid.Empty || !TryNormalizeHex(color.Hex, out var normalized)) { error = "ColorInvalid"; return false; }
                color.Hex = normalized;
            }
        }
        return true;
    }

    private static string GetContentType(string extension) => extension.ToLowerInvariant() switch { ".png" => "image/png", ".jpg" or ".jpeg" => "image/jpeg", ".webp" => "image/webp", ".gif" => "image/gif", ".bmp" => "image/bmp", _ => "image/tiff" };
}

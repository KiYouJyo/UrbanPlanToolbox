namespace UrbanPlanToolbox.Services;

/// <summary>Converts XAML property-resource keys to MRT Core dynamic paths.</summary>
public static class MrtResourceKeyNormalizer
{
    public static string Normalize(string resourceKey)
    {
        if (string.IsNullOrWhiteSpace(resourceKey)) return resourceKey;

        foreach (var property in new[] { ".Text", ".Content", ".Header", ".PlaceholderText", ".Title", ".Message" })
        {
            if (resourceKey.EndsWith(property, StringComparison.Ordinal))
                return resourceKey[..^property.Length] + "/" + property[1..];
        }

        return resourceKey;
    }
}

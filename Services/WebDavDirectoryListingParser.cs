using System.Globalization;
using System.Xml.Linq;
using UrbanPlanToolbox.Models;

namespace UrbanPlanToolbox.Services;

public static class WebDavDirectoryListingParser
{
    public static IReadOnlyList<CloudBackupItem> Parse(string xml)
    {
        ArgumentNullException.ThrowIfNull(xml);
        var document = XDocument.Parse(xml, LoadOptions.None);
        var items = new List<CloudBackupItem>();

        foreach (var responseElement in document.Descendants().Where(element => NameEquals(element, "response")))
        {
            var properties = GetSuccessfulProperties(responseElement).ToArray();
            if (properties.Length == 0)
                properties = responseElement.Descendants().Where(element => NameEquals(element, "prop")).ToArray();

            if (properties.SelectMany(property => property.Descendants()).Any(element => NameEquals(element, "collection")))
                continue;

            var displayName = FirstPropertyValue(properties, "displayname");
            var href = responseElement.Descendants().FirstOrDefault(element => NameEquals(element, "href"))?.Value;
            var fileName = ResolveBackupFileName(displayName, href);
            if (fileName is null)
                continue;

            _ = long.TryParse(
                FirstPropertyValue(properties, "getcontentlength"),
                NumberStyles.Integer,
                CultureInfo.InvariantCulture,
                out var size);

            DateTimeOffset? lastModified = null;
            if (DateTimeOffset.TryParse(
                FirstPropertyValue(properties, "getlastmodified"),
                CultureInfo.InvariantCulture,
                DateTimeStyles.AssumeUniversal,
                out var parsedModified))
            {
                lastModified = parsedModified.ToUniversalTime();
            }

            DateTimeOffset? created = null;
            string? version = null;
            if (CloudBackupItem.TryParseFileName(fileName, out var parsedCreated, out var parsedVersion))
            {
                created = parsedCreated;
                version = parsedVersion;
            }

            items.Add(new CloudBackupItem(fileName, Math.Max(0, size), created, lastModified, version));
        }

        return items
            .GroupBy(item => item.FileName, StringComparer.OrdinalIgnoreCase)
            .Select(group => group.OrderByDescending(item => item.SortTimeUtc).First())
            .OrderByDescending(item => item.SortTimeUtc)
            .ToArray();
    }

    public static bool IsBackupFileName(string? fileName) =>
        !string.IsNullOrWhiteSpace(fileName) &&
        fileName.IndexOfAny(Path.GetInvalidFileNameChars()) < 0 &&
        !fileName.Contains('/') &&
        !fileName.Contains('\\') &&
        fileName.EndsWith(".uptbackup", StringComparison.OrdinalIgnoreCase);

    private static IEnumerable<XElement> GetSuccessfulProperties(XElement responseElement)
    {
        foreach (var propStat in responseElement.Descendants().Where(element => NameEquals(element, "propstat")))
        {
            var statusText = propStat.Descendants().FirstOrDefault(element => NameEquals(element, "status"))?.Value;
            if (!IsSuccessfulStatus(statusText))
                continue;

            foreach (var property in propStat.Elements().Where(element => NameEquals(element, "prop")))
                yield return property;
        }
    }

    private static bool IsSuccessfulStatus(string? statusText)
    {
        if (string.IsNullOrWhiteSpace(statusText))
            return true;

        foreach (var token in statusText.Split(' ', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
        {
            if (token.Length == 3 && int.TryParse(token, NumberStyles.None, CultureInfo.InvariantCulture, out var statusCode))
                return statusCode is >= 200 and < 300;
        }

        return false;
    }

    private static string? FirstPropertyValue(IEnumerable<XElement> properties, string localName) =>
        properties
            .SelectMany(property => property.Elements())
            .FirstOrDefault(element => NameEquals(element, localName))?
            .Value;

    private static string? ResolveBackupFileName(string? displayName, string? href)
    {
        var fromDisplayName = NormalizeCandidate(displayName);
        if (IsBackupFileName(fromDisplayName))
            return fromDisplayName;

        if (string.IsNullOrWhiteSpace(href))
            return null;

        var rawPath = href.Trim();
        if (Uri.TryCreate(rawPath, UriKind.Absolute, out var absoluteUri))
        {
            rawPath = absoluteUri.AbsolutePath;
        }
        else
        {
            var queryIndex = rawPath.IndexOfAny(['?', '#']);
            if (queryIndex >= 0)
                rawPath = rawPath[..queryIndex];
        }

        rawPath = rawPath.Replace('\\', '/').TrimEnd('/');
        var segment = rawPath.Split('/', StringSplitOptions.RemoveEmptyEntries).LastOrDefault();
        var fromHref = NormalizeCandidate(segment);
        return IsBackupFileName(fromHref) ? fromHref : null;
    }

    private static string? NormalizeCandidate(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
            return null;

        var candidate = value.Trim().Replace('\\', '/').TrimEnd('/').Split('/').LastOrDefault();
        if (string.IsNullOrWhiteSpace(candidate))
            return null;

        try { return Uri.UnescapeDataString(candidate); }
        catch (UriFormatException) { return candidate; }
    }

    private static bool NameEquals(XElement element, string localName) =>
        string.Equals(element.Name.LocalName, localName, StringComparison.OrdinalIgnoreCase);
}

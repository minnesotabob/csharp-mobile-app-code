using System.Text.Json;
using System.Text.Json.Serialization;

namespace GnazzosWorld.Core.Catalog;

public static class CatalogLoader
{
    private static readonly JsonSerializerOptions Options = new()
    {
        PropertyNameCaseInsensitive = true,
        Converters = { new JsonStringEnumConverter<ContentKind>() }
    };

    public static IReadOnlyList<CatalogItem> Load(Stream json)
    {
        ArgumentNullException.ThrowIfNull(json);
        var items = JsonSerializer.Deserialize<List<CatalogItem>>(json, Options)
            ?? throw new InvalidDataException("The catalog must be an array.");
        var ids = new HashSet<string>(StringComparer.Ordinal);
        foreach (var item in items)
        {
            if (item is null || string.IsNullOrWhiteSpace(item.Id)
                || string.IsNullOrWhiteSpace(item.Title)
                || string.IsNullOrWhiteSpace(item.Creator)
                || string.IsNullOrWhiteSpace(item.Description)
                || !Enum.IsDefined(item.Kind))
                throw new InvalidDataException("Every item needs valid catalog metadata.");
            if (!ids.Add(item.Id))
                throw new InvalidDataException($"Duplicate catalog ID: {item.Id}");
            if (item.ExternalUrl is { } url
                && (!url.IsAbsoluteUri || url.Scheme != Uri.UriSchemeHttps))
                throw new InvalidDataException("External links must be absolute HTTPS URLs.");
        }
        return items.AsReadOnly();
    }

    public static IReadOnlyList<CatalogItem> LoadBundled()
    {
        using var stream = typeof(CatalogLoader).Assembly.GetManifestResourceStream(
            "GnazzosWorld.Core.Data.catalog.json")
            ?? throw new InvalidOperationException("Bundled catalog is missing.");
        return Load(stream);
    }
}

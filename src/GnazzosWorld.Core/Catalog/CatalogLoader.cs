using System.Text.Json;
using System.Text.Json.Serialization;

namespace GnazzosWorld.Core.Catalog;

public static class CatalogLoader
{
    private static readonly JsonSerializerOptions Options = new()
    {
        PropertyNameCaseInsensitive = true,
        Converters =
        {
            new JsonStringEnumConverter<ContentKind>(),
            new JsonStringEnumConverter<ContentAvailability>(),
            new JsonStringEnumConverter<ContentAdvisory>()
        }
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
            if (!Enum.IsDefined(item.Availability) || !Enum.IsDefined(item.Advisory)
                || item.CreatorId is not null && string.IsNullOrWhiteSpace(item.CreatorId)
                || item.Edition is not null && (string.IsNullOrWhiteSpace(item.Edition)
                    || item.Kind != ContentKind.Book)
                || item.DurationSeconds is not null && (item.DurationSeconds <= 0
                    || item.Kind == ContentKind.Book))
                throw new InvalidDataException("Invalid optional catalog metadata.");
            if (!ids.Add(item.Id))
                throw new InvalidDataException($"Duplicate catalog ID: {item.Id}");
            if (item.ExternalUrl is { } url
                && (!url.IsAbsoluteUri || url.Scheme != Uri.UriSchemeHttps))
                throw new InvalidDataException("External links must be absolute HTTPS URLs.");
        }
        return items.AsReadOnly();
    }

    public static IReadOnlyList<CatalogItem> LoadBundled()
        => CatalogSnapshot.LoadBundled().Items;
}

using System.Text.Json;
using System.Text.Json.Serialization;

namespace GnazzosWorld.Core.Catalog;

public enum CreatorKind { Artist, Author, Host }

public sealed record CatalogCreator(
    [property: JsonRequired] string Id,
    [property: JsonRequired] string Name,
    [property: JsonRequired] CreatorKind Kind);

// A coherent import boundary. Titles and display names are never identity keys.
public sealed record CatalogSnapshot(
    IReadOnlyList<CatalogItem> Items,
    IReadOnlyList<CatalogCreator> Creators)
{
    private static readonly JsonSerializerOptions Options = new()
    {
        PropertyNameCaseInsensitive = true,
        Converters = { new JsonStringEnumConverter<CreatorKind>() }
    };

    // Streams remain owned by the caller, including when validation fails.
    public static CatalogSnapshot Load(Stream itemsJson, Stream creatorsJson)
    {
        ArgumentNullException.ThrowIfNull(itemsJson);
        ArgumentNullException.ThrowIfNull(creatorsJson);
        var items = CatalogLoader.Load(itemsJson);
        var creators = JsonSerializer.Deserialize<List<CatalogCreator>>(creatorsJson, Options)
            ?? throw new InvalidDataException("Creators must be an array.");
        return Create(items, creators);
    }

    public static CatalogSnapshot Create(IEnumerable<CatalogItem> itemSource, IEnumerable<CatalogCreator> creatorSource)
    {
        var items = CatalogLoader.Validate(itemSource);
        var creators = creatorSource.ToList();
        var ids = new HashSet<string>(StringComparer.Ordinal);
        foreach (var creator in creators)
        {
            if (creator is null || string.IsNullOrWhiteSpace(creator.Id)
                || string.IsNullOrWhiteSpace(creator.Name) || !Enum.IsDefined(creator.Kind))
                throw new InvalidDataException("Every creator needs valid metadata.");
            if (!ids.Add(creator.Id))
                throw new InvalidDataException($"Duplicate creator ID: {creator.Id}");
        }
        foreach (var item in items)
            if (item.CreatorId is { } id && !ids.Contains(id))
                throw new InvalidDataException($"Unknown creator ID: {id}");

        return new CatalogSnapshot(items, creators.AsReadOnly());
    }

    public static CatalogSnapshot LoadBundled()
    {
        using var items = OpenResource("catalog.json");
        using var creators = OpenResource("creators.json");
        return Load(items, creators);
    }

    private static Stream OpenResource(string name)
        => typeof(CatalogSnapshot).Assembly.GetManifestResourceStream(
            $"GnazzosWorld.Core.Data.{name}")
            ?? throw new InvalidOperationException($"Bundled resource is missing: {name}");
}

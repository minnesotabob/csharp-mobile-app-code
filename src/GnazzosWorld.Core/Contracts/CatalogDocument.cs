using System.Text.Json;
using System.Text.Json.Serialization;
using GnazzosWorld.Core.Catalog;
using GnazzosWorld.Core.Networking;

namespace GnazzosWorld.Core.Contracts;

// Explicit public wire envelope. V1 shares the teaching catalog records deliberately.
public sealed record CatalogDocument(
    [property: JsonRequired] int SchemaVersion,
    [property: JsonRequired] string Revision,
    [property: JsonRequired] IReadOnlyList<CatalogItem> Items,
    [property: JsonRequired] IReadOnlyList<CatalogCreator> Creators)
{
    public static bool IsValidRevision(string? value) => value is { Length: > 0 and <= 80 }
        && value.All(c => char.IsAsciiLetterOrDigit(c) || c is '.' or '_' or '-');

    public CatalogSnapshot Validate()
    {
        if (SchemaVersion != 1 || !IsValidRevision(Revision) || Items is null || Creators is null
            || Items.Count > 1000 || Creators.Count > 1000)
            throw new InvalidDataException("Unsupported or invalid catalog document.");
        return CatalogSnapshot.Create(Items, Creators);
    }
    public static CatalogDocument Read(Stream json)
    {
        var options = new JsonSerializerOptions(JsonSerializerDefaults.Web);
        options.Converters.Add(new JsonStringEnumConverter());
        var document = JsonSerializer.Deserialize<CatalogDocument>(json, options)
            ?? throw new InvalidDataException("Catalog document is required.");
        var snapshot = document.Validate();
        return document with { Items = snapshot.Items, Creators = snapshot.Creators };
    }
}

public sealed record CatalogPageDto(int SchemaVersion, string Revision, int Offset,
    int Limit, int Total, int? NextOffset, IReadOnlyList<CatalogItem> Items);

public sealed class HttpCatalogDocumentSource(HttpClient client, Uri endpoint, TimeProvider clock)
{
    private readonly BoundedJsonClient transport = new(client, endpoint, clock);
    public Task<CatalogDocument> LoadAsync(CancellationToken cancellationToken = default)
        => transport.GetAsync(CatalogDocument.Read, cancellationToken);
}

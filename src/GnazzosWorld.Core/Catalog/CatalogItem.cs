using System.Text.Json.Serialization;

namespace GnazzosWorld.Core.Catalog;

public enum ContentKind { Book, Song, Podcast }

public sealed record CatalogItem(
    [property: JsonRequired] string Id,
    [property: JsonRequired] string Title,
    [property: JsonRequired] string Creator,
    [property: JsonRequired] ContentKind Kind,
    [property: JsonRequired] string Description,
    Uri? ExternalUrl = null);

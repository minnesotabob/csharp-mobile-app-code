using System.Text.Json.Serialization;

namespace GnazzosWorld.Core.Catalog;

public enum ContentKind { Book, Song, Podcast }
public enum ContentAvailability { Unknown, Listed, Unavailable }
public enum ContentAdvisory { Unknown, Clean, Explicit }

public sealed record CatalogItem(
    [property: JsonRequired] string Id,
    [property: JsonRequired] string Title,
    [property: JsonRequired] string Creator,
    [property: JsonRequired] ContentKind Kind,
    [property: JsonRequired] string Description,
    Uri? ExternalUrl = null,
    string? CreatorId = null,
    DateOnly? ReleaseDate = null,
    string? Edition = null,
    int? DurationSeconds = null,
    ContentAvailability Availability = ContentAvailability.Unknown,
    ContentAdvisory Advisory = ContentAdvisory.Unknown,
    ReadingSample? Sample = null);

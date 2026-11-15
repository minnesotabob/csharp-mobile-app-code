namespace GnazzosWorld.Core.Catalog;

public enum ContentKind { Book, Song, Podcast }

public sealed record CatalogItem(
    string Id, string Title, string Creator, ContentKind Kind,
    string Description, Uri? ExternalUrl = null);

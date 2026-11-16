namespace GnazzosWorld.Core.Catalog;

/// <summary>Two fixed orderings of trusted, immutable catalog records. Rebuild on catalog revision.</summary>
public sealed class CatalogSearchSnapshot
{
    private readonly CatalogItem[] title;
    private readonly CatalogItem[] newest;
    public IReadOnlyList<CatalogItem> Items { get; }
    public CatalogSearchSnapshot(IEnumerable<CatalogItem> source)
    {
        ArgumentNullException.ThrowIfNull(source);
        var copy = source.ToArray();
        title = CatalogSearch.Find(copy, null).ToArray();
        newest = CatalogSearch.Find(copy, null, sort: CatalogSort.NewestFirst).ToArray();
        Items = Array.AsReadOnly(title);
    }
    public IReadOnlyList<CatalogItem> Find(string? query, ContentKind? kind = null,
        string? creatorId = null, CatalogSort sort = CatalogSort.Title,
        IReadOnlySet<string>? favoriteIds = null)
        => CatalogSearch.Filter(sort == CatalogSort.NewestFirst ? newest : title,
            query, kind, creatorId, sort, favoriteIds).ToArray();
}

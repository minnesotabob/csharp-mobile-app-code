using System.Globalization;

namespace GnazzosWorld.Core.Catalog;

public enum CatalogSort { Title, NewestFirst }

public static class CatalogSearch
{
    public static IReadOnlyList<CatalogItem> Find(
        IEnumerable<CatalogItem> source, string? query, ContentKind? kind = null,
        string? creatorId = null, CatalogSort sort = CatalogSort.Title,
        IReadOnlySet<string>? favoriteIds = null)
    {
        ArgumentNullException.ThrowIfNull(source);
        if (kind.HasValue && !Enum.IsDefined(kind.Value))
            throw new ArgumentOutOfRangeException(nameof(kind));
        if (!Enum.IsDefined(sort))
            throw new ArgumentOutOfRangeException(nameof(sort));
        var term = query?.Trim() ?? string.Empty;
        var comparer = CultureInfo.InvariantCulture.CompareInfo;
        var matches = source.Where(item => (!kind.HasValue || item.Kind == kind)
            && (creatorId is null || StringComparer.Ordinal.Equals(item.CreatorId, creatorId))
            && (favoriteIds is null || favoriteIds.Contains(item.Id))
            && (term.Length == 0
                || comparer.IndexOf(item.Title, term,
                    CompareOptions.IgnoreCase | CompareOptions.IgnoreNonSpace) >= 0
                || comparer.IndexOf(item.Creator, term,
                    CompareOptions.IgnoreCase | CompareOptions.IgnoreNonSpace) >= 0));
        return (sort == CatalogSort.NewestFirst
            ? matches.OrderBy(x => x.ReleaseDate is null)
                .ThenByDescending(x => x.ReleaseDate)
                .ThenBy(x => x.Title, StringComparer.OrdinalIgnoreCase)
                .ThenBy(x => x.Id, StringComparer.Ordinal)
            : matches.OrderBy(x => x.Title, StringComparer.OrdinalIgnoreCase)
                .ThenBy(x => x.Id, StringComparer.Ordinal)).ToArray();
    }
}

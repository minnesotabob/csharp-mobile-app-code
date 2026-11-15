using System.Globalization;

namespace GnazzosWorld.Core.Catalog;

public static class CatalogSearch
{
    public static IReadOnlyList<CatalogItem> Find(
        IEnumerable<CatalogItem> source, string? query, ContentKind? kind = null)
    {
        ArgumentNullException.ThrowIfNull(source);
        var term = query?.Trim() ?? string.Empty;
        var comparer = CultureInfo.InvariantCulture.CompareInfo;
        return source.Where(item => (!kind.HasValue || item.Kind == kind)
            && (term.Length == 0
                || comparer.IndexOf(item.Title, term,
                    CompareOptions.IgnoreCase | CompareOptions.IgnoreNonSpace) >= 0
                || comparer.IndexOf(item.Creator, term,
                    CompareOptions.IgnoreCase | CompareOptions.IgnoreNonSpace) >= 0))
            .OrderBy(item => item.Title, StringComparer.OrdinalIgnoreCase)
            .ThenBy(item => item.Id, StringComparer.Ordinal)
            .ToArray();
    }
}

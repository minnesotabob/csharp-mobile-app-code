using System.Globalization;
using GnazzosWorld.Core.Catalog;

namespace GnazzosWorld.Core.Performance;

/// <summary>Generated public synthetic metadata, never customer content or an API import.</summary>
public static class PerformanceCatalogFixture
{
    public const string Version = "catalog-perf-v1";
    public const int MaximumItems = 20000;
    public static IReadOnlyList<CatalogItem> Create(int count = 10000)
    {
        if (count is < 1 or > MaximumItems) throw new ArgumentOutOfRangeException(nameof(count));
        var items = new CatalogItem[count];
        // Reverse input order, repeated title/date keys, accents, unknown dates and long display text.
        for (var i = 0; i < count; i++)
        {
            var n = count - i - 1;
            var title = (n % 4) switch
            {
                0 => "Synthetic Café – a long invented title for text expansion and narrow-screen review",
                1 => "Synthetic Be\u0301ret violet",
                2 => "Synthetic canción del lago",
                _ => "Synthetic 物語と音楽"
            };
            items[i] = new("perf-" + n.ToString("D5", CultureInfo.InvariantCulture), title,
                "Synthetic creator " + (n % 16).ToString("D2", CultureInfo.InvariantCulture),
                (ContentKind)(n % 3), "Generated public performance fixture, no private story.",
                CreatorId: "perf-creator-" + (n % 16).ToString("D2", CultureInfo.InvariantCulture),
                ReleaseDate: n % 5 == 0 ? null : new DateOnly(2020 + n % 7, 1 + n % 12, 1 + n % 28));
        }
        return Array.AsReadOnly(items);
    }
}

using GnazzosWorld.Core.Catalog;
using GnazzosWorld.Core.Performance;
using GnazzosWorld.Core.Presentation;
using Xunit;

namespace GnazzosWorld.Core.Tests;

public sealed class PerformanceTests
{
    [Theory]
    [InlineData(0)][InlineData(20001)]
    public void Fixture_rejects_unbounded_counts(int count)
        => Assert.Throws<ArgumentOutOfRangeException>(() => PerformanceCatalogFixture.Create(count));

    [Fact]
    public void Fixture_has_deterministic_public_records_and_varied_stress_dimensions()
    {
        var a = PerformanceCatalogFixture.Create(1000); var b = PerformanceCatalogFixture.Create(1000);
        Assert.Equal(a, b); Assert.Equal(1000, a.Select(x => x.Id).Distinct().Count());
        Assert.Equal(3, a.Select(x => x.Kind).Distinct().Count()); Assert.Equal(16, a.Select(x => x.CreatorId).Distinct().Count());
        Assert.Contains(a, x => x.ReleaseDate is null); Assert.Contains(a, x => x.Title.Length > 70);
    }
    [Theory]
    [InlineData(CatalogSort.Title)][InlineData(CatalogSort.NewestFirst)]
    public void Snapshot_matches_reference_for_queries_kinds_creators_and_favorites(CatalogSort sort)
    {
        var items = PerformanceCatalogFixture.Create(1200); var snapshot = new CatalogSearchSnapshot(items);
        IReadOnlySet<string> favorites = new HashSet<string>(items.Where((_, i) => i % 7 == 0).Select(x => x.Id));
        foreach (var query in new string?[] { null, "", " SYNTHETIC ", "cafe", "BÉRET", "物語", "no match" })
        foreach (var kind in new ContentKind?[] { null, ContentKind.Book, ContentKind.Song, ContentKind.Podcast })
        foreach (var creator in new string?[] { null, "perf-creator-02", "missing" })
        foreach (var selected in new IReadOnlySet<string>?[] { null, favorites, new HashSet<string>() })
            Assert.Equal(CatalogSearch.Find(items, query, kind, creator, sort, selected).Select(x => x.Id),
                snapshot.Find(query, kind, creator, sort, selected).Select(x => x.Id));
    }
    [Fact]
    public void Snapshot_owns_its_orderings_and_result_arrays_cannot_mutate_the_source()
    {
        var source = PerformanceCatalogFixture.Create(10).ToArray(); var first = source[0];
        var snapshot = new CatalogSearchSnapshot(source); source[0] = source[0] with { Title = "Changed later" };
        Assert.Contains(first, snapshot.Items); Assert.DoesNotContain(snapshot.Items, x => x.Title == "Changed later");
        var result = Assert.IsType<CatalogItem[]>(snapshot.Find(null)); result[0] = result[0] with { Title = "Result mutation" };
        Assert.DoesNotContain(snapshot.Find(null), x => x.Title == "Result mutation");
        Assert.Throws<NotSupportedException>(() => ((IList<CatalogItem>)snapshot.Items)[0] = first);
    }
    [Fact]
    public void Snapshot_rejects_invalid_filters_and_supports_empty_source()
    {
        var snapshot = new CatalogSearchSnapshot([]); Assert.Empty(snapshot.Find(null));
        Assert.Throws<ArgumentOutOfRangeException>(() => snapshot.Find(null, (ContentKind)99));
        Assert.Throws<ArgumentOutOfRangeException>(() => snapshot.Find(null, sort: (CatalogSort)99));
        Assert.Throws<ArgumentNullException>(() => new CatalogSearchSnapshot(null!));
    }
    [Fact]
    public void Browser_uses_one_stable_snapshot_and_preserves_selection_contract()
    {
        var source = PerformanceCatalogFixture.Create(100).ToArray(); var vm = new CatalogBrowserViewModel(source, new Navigation());
        var item = vm.Items.First(x => x.Title.Contains("Café", StringComparison.Ordinal)); vm.SelectedItem = item;
        source[0] = source[0] with { Title = "Later source replacement" };
        vm.Query = "cafe"; Assert.Equal(item.Id, vm.SelectedItem!.Id); Assert.All(vm.Items, x => Assert.Contains("Café", x.Title));
        vm.Query = "no matches"; Assert.Null(vm.SelectedItem); Assert.False(vm.OpenDetails.CanExecute(null));
    }
    private sealed class Navigation : ICatalogNavigation
    { public Task OpenItemAsync(string id) => Task.CompletedTask; }
}

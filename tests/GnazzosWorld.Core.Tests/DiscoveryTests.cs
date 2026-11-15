using GnazzosWorld.Core.Catalog;
using GnazzosWorld.Core.Presentation;
using Xunit;

namespace GnazzosWorld.Core.Tests;

public sealed class DiscoveryTests
{
    private static CatalogItem Item(string id, string title = "Same",
        string creator = "Creator", string creatorId = "artist",
        DateOnly? date = null) => new(id, title, creator, ContentKind.Song, "D",
            CreatorId: creatorId, ReleaseDate: date);

    [Fact]
    public void Search_matches_composed_and_decomposed_accents()
    {
        var items = new[] { Item("x", "Be\u0301ret violet") };
        Assert.Equal("x", Assert.Single(CatalogSearch.Find(items, " BÉRET ")).Id);
        Assert.Equal("x", Assert.Single(CatalogSearch.Find(items, "beret")).Id);
    }

    [Fact]
    public void Creator_identity_filters_equal_display_names_separately()
    {
        var items = new[] { Item("a", creatorId: "first"), Item("b", creatorId: "second") };
        Assert.Equal("b", Assert.Single(CatalogSearch.Find(items, null,
            creatorId: "second")).Id);
        Assert.Empty(CatalogSearch.Find(items, null, creatorId: "SECOND"));
    }

    [Fact]
    public void Search_intersects_type_creator_and_favorites()
    {
        var a = Item("a", "North");
        var b = Item("b", "North") with { Kind = ContentKind.Book };
        IReadOnlySet<string> favorites = new HashSet<string> { "a", "b" };
        Assert.Equal("a", Assert.Single(CatalogSearch.Find([a, b], "north",
            ContentKind.Song, "artist", favoriteIds: favorites)).Id);
        Assert.Empty(CatalogSearch.Find([a, b], "south", favoriteIds: favorites));
    }

    [Fact]
    public void Newest_sort_puts_unknown_dates_last_and_breaks_ties_by_id()
    {
        var items = new[] {
            Item("unknown"), Item("old", date: new DateOnly(2020, 1, 1)),
            Item("b", date: new DateOnly(2026, 1, 1)),
            Item("a", date: new DateOnly(2026, 1, 1)) };
        Assert.Equal(new[] { "a", "b", "old", "unknown" },
            CatalogSearch.Find(items, null, sort: CatalogSort.NewestFirst).Select(x => x.Id));
    }

    [Fact]
    public void Default_sort_remains_stable_for_equal_titles()
        => Assert.Equal(new[] { "a", "b" },
            CatalogSearch.Find(new[] { Item("b"), Item("a") }, null).Select(x => x.Id));

    [Fact]
    public void Empty_favorites_filter_matches_nothing()
        => Assert.Empty(CatalogSearch.Find(new[] { Item("a") }, null,
            favoriteIds: new HashSet<string>()));

    [Fact]
    public void Invalid_sort_and_content_kind_are_rejected()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() =>
            CatalogSearch.Find([], null, sort: (CatalogSort)99));
        Assert.Throws<ArgumentOutOfRangeException>(() =>
            CatalogSearch.Find([], null, kind: (ContentKind)99));
    }

    private sealed class Store : IFavoriteStore
    {
        public IReadOnlyList<string> Saved { get; set; } = [];
        public bool FailLoad { get; set; }
        public bool FailSave { get; set; }
        public IReadOnlyList<string> Load() => FailLoad
            ? throw new IOException("fixture") : Saved;
        public void Save(IReadOnlyList<string> ids)
        {
            if (FailSave) throw new IOException("fixture");
            Saved = ids.ToArray();
        }
    }

    [Fact]
    public void Favorites_survive_a_new_service_instance_and_title_changes()
    {
        var store = new Store();
        var first = new FavoriteCatalog(store);
        first.Reload(); first.Toggle("song");
        var restarted = new FavoriteCatalog(store);
        restarted.Reload();
        Assert.True(restarted.Contains("song"));
        var renamed = Item("song", "Renamed title");
        Assert.Single(CatalogSearch.Find([renamed], null,
            favoriteIds: restarted.Snapshot()));
    }

    [Fact]
    public void Failed_save_preserves_previous_visible_favorites()
    {
        var store = new Store { Saved = new[] { "existing" } };
        var favorites = new FavoriteCatalog(store);
        favorites.Reload(); store.FailSave = true;
        Assert.Throws<IOException>(() => favorites.Toggle("new"));
        Assert.True(favorites.Contains("existing"));
        Assert.False(favorites.Contains("new"));
        Assert.Equal(new[] { "existing" }, store.Saved);
    }

    [Fact]
    public void Failed_reload_preserves_the_last_good_snapshot()
    {
        var store = new Store { Saved = new[] { "existing" } };
        var favorites = new FavoriteCatalog(store);
        favorites.Reload(); store.FailLoad = true;
        Assert.Throws<IOException>(() => favorites.Reload());
        Assert.True(favorites.IsLoaded);
        Assert.True(favorites.Contains("existing"));
    }

    [Fact]
    public void Initially_failed_load_does_not_allow_an_overwriting_edit()
    {
        var store = new Store { FailLoad = true };
        var favorites = new FavoriteCatalog(store);
        Assert.Throws<IOException>(() => favorites.Reload());
        Assert.False(favorites.IsLoaded);
        Assert.Throws<InvalidOperationException>(() => favorites.Toggle("new"));
        store.FailLoad = false; favorites.Reload(); favorites.Toggle("new");
        Assert.True(favorites.Contains("new"));
    }

    [Fact]
    public void Removing_a_favorite_persists_without_discarding_unknown_ids()
    {
        var store = new Store { Saved = new[] { "a", "not-in-current-catalog" } };
        var favorites = new FavoriteCatalog(store);
        favorites.Reload(); favorites.Toggle("a");
        Assert.Equal(new[] { "not-in-current-catalog" }, store.Saved);
    }

    [Fact]
    public void Snapshot_cannot_mutate_the_service()
    {
        var favorites = new FavoriteCatalog(new MemoryFavoriteStore());
        favorites.Reload(); favorites.Toggle("a");
        var copy = Assert.IsType<HashSet<string>>(favorites.Snapshot());
        copy.Clear();
        Assert.True(favorites.Contains("a"));
    }

    [Theory]
    [InlineData("null")]
    [InlineData("{}")]
    [InlineData("[null]")]
    [InlineData("[\" \"]")]
    [InlineData("[42]")]
    [InlineData("[")]
    public void Invalid_saved_favorites_are_reported(string json)
        => Assert.Throws<InvalidDataException>(() => FavoriteIdCodec.Decode(json));

    [Fact]
    public void Favorite_codec_round_trip_deduplicates_and_preserves_ordinal_identity()
    {
        var json = FavoriteIdCodec.Encode(new[] { "z", "A", "a", "A" });
        Assert.Equal(new[] { "A", "a", "z" }, FavoriteIdCodec.Decode(json));
    }

    [Fact]
    public void Favorite_codec_bounds_count_id_length_and_payload_size()
    {
        Assert.Throws<InvalidDataException>(() => FavoriteIdCodec.Encode(
            Enumerable.Range(0, 33).Select(x => $"id-{x}")));
        Assert.Throws<InvalidDataException>(() =>
            FavoriteIdCodec.Encode([new string('x', 129)]));
        Assert.Throws<InvalidDataException>(() =>
            FavoriteIdCodec.Decode(new string(' ', 4097)));
    }

    private sealed class Navigation : ICatalogNavigation
    {
        public Task OpenItemAsync(string id) => Task.CompletedTask;
    }

    [Fact]
    public async Task Removing_selected_favorite_from_filtered_results_clears_details()
    {
        var vm = new CatalogBrowserViewModel(new[] { Item("a") }, new Navigation());
        vm.SelectedItem = vm.Items[0];
        await vm.ToggleFavorite.ExecuteAsync();
        vm.FavoritesOnly = true;
        Assert.Single(vm.Items);
        await vm.ToggleFavorite.ExecuteAsync();
        Assert.Empty(vm.Items);
        Assert.Null(vm.SelectedItem);
        Assert.False(vm.Detail.HasItem);
        Assert.False(vm.OpenDetails.CanExecute(null));
    }

    [Fact]
    public async Task View_model_reports_storage_failure_and_recovers_on_retry()
    {
        var store = new Store { FailLoad = true };
        var vm = new CatalogBrowserViewModel(new[] { Item("a") }, new Navigation(),
            new FavoriteCatalog(store));
        Assert.Single(vm.Items);
        Assert.NotEmpty(vm.FavoritesError);
        vm.SelectedItem = vm.Items[0];
        Assert.False(vm.ToggleFavorite.CanExecute(null));
        store.FailLoad = false;
        await vm.RetryFavorites.ExecuteAsync();
        Assert.Empty(vm.FavoritesError);
        Assert.True(vm.ToggleFavorite.CanExecute(null));
        store.FailSave = true;
        await vm.ToggleFavorite.ExecuteAsync();
        Assert.NotEmpty(vm.FavoritesError);
        Assert.Empty(store.Saved);
    }

    [Fact]
    public void View_model_combines_creator_selection_with_other_criteria()
    {
        var vm = new CatalogBrowserViewModel(
            new[] { Item("a", creator: "First", creatorId: "first"),
                    Item("b", creator: "Second", creatorId: "second") }, new Navigation());
        vm.SelectedItem = vm.Items[0];
        vm.CreatorIndex = 2;
        Assert.Equal("b", Assert.Single(vm.Items).Id);
        Assert.Null(vm.SelectedItem);
        vm.SortIndex = 1;
        Assert.Equal("b", Assert.Single(vm.Items).Id);
        vm.CreatorIndex = -1;
        vm.SortIndex = -1;
        Assert.Equal(0, vm.CreatorIndex);
        Assert.Equal(0, vm.SortIndex);
        Assert.Throws<ArgumentOutOfRangeException>(() => vm.CreatorIndex = 99);
        Assert.Throws<ArgumentOutOfRangeException>(() => vm.SortIndex = 2);
    }

    private sealed class ManualDelay
    {
        public List<(TaskCompletionSource Completion, CancellationToken Token)> Calls { get; } = [];
        public Task Wait(TimeSpan _, CancellationToken token)
        {
            var completion = new TaskCompletionSource(
                TaskCreationOptions.RunContinuationsAsynchronously);
            Calls.Add((completion, token));
            // Deliberately ignores cancellation to expose stale-completion bugs.
            return completion.Task;
        }
    }

    [Fact]
    public async Task Latest_input_wins_when_older_delay_completes_last()
    {
        var delay = new ManualDelay();
        var input = new DebouncedQuery(delay.Wait);
        var applied = new List<string>();
        var first = input.UpdateAsync("k", applied.Add, () => Assert.Fail("Unexpected error"));
        var second = input.UpdateAsync("kai", applied.Add, () => Assert.Fail("Unexpected error"));
        Assert.True(delay.Calls[0].Token.IsCancellationRequested);
        delay.Calls[1].Completion.SetResult(); await second;
        delay.Calls[0].Completion.SetResult(); await first;
        Assert.Equal(new[] { "kai" }, applied);
    }

    [Fact]
    public async Task Clearing_is_immediate_and_invalidates_pending_input()
    {
        var delay = new ManualDelay();
        var input = new DebouncedQuery(delay.Wait);
        var applied = new List<string>();
        var pending = input.UpdateAsync("old", applied.Add, () => { });
        await input.UpdateAsync("", applied.Add, () => { });
        delay.Calls[0].Completion.SetResult(); await pending;
        Assert.Equal(new[] { "" }, applied);
    }

    [Fact]
    public async Task Leaving_the_page_prevents_late_application()
    {
        var delay = new ManualDelay();
        var input = new DebouncedQuery(delay.Wait);
        var applied = new List<string>();
        var pending = input.UpdateAsync("old", applied.Add, () => { });
        input.Cancel();
        delay.Calls[0].Completion.SetResult(); await pending;
        Assert.Empty(applied);
    }

    [Fact]
    public async Task Stale_errors_are_ignored_and_current_errors_are_reported()
    {
        var delay = new ManualDelay();
        var input = new DebouncedQuery(delay.Wait);
        var errors = 0;
        var first = input.UpdateAsync("old", _ => { }, () => errors++);
        var second = input.UpdateAsync("new", _ => { }, () => errors++);
        delay.Calls[0].Completion.SetException(new IOException("old")); await first;
        Assert.Equal(0, errors);
        delay.Calls[1].Completion.SetException(new IOException("new")); await second;
        Assert.Equal(1, errors);
        await input.UpdateAsync(null, _ => { }, () => errors++);
        Assert.Equal(1, errors);
    }

    [Fact]
    public async Task Cooperative_cancellation_is_observed_without_an_error()
    {
        var entered = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var input = new DebouncedQuery(async (_, token) =>
        {
            entered.SetResult();
            await Task.Delay(Timeout.InfiniteTimeSpan, token);
        });
        var applied = new List<string>();
        var errors = 0;
        var pending = input.UpdateAsync("old", applied.Add, () => errors++);
        await entered.Task;
        input.Cancel(); await pending;
        Assert.Empty(applied);
        Assert.Equal(0, errors);
    }
}

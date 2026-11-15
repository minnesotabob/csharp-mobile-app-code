using System.Globalization;
using GnazzosWorld.Core.Catalog;
using GnazzosWorld.Core.Localization;

namespace GnazzosWorld.Core.Presentation;

public sealed class CatalogBrowserViewModel : ObservableObject
{
    private readonly IReadOnlyList<CatalogItem> catalog;
    private readonly FavoriteCatalog favorites;
    private readonly string?[] creatorIds;
    private int creatorIndex;
    private int sortIndex;
    private bool favoritesOnly;
    private bool filtersExpanded;
    private string favoritesError = "";
    private string searchError = "";
    private string query = "";
    private int filterIndex;
    private CatalogItem? selectedItem;
    private string navigationError = "";

    public CatalogBrowserViewModel(
        IReadOnlyList<CatalogItem> catalog, ICatalogNavigation navigation,
        FavoriteCatalog? favorites = null)
    {
        this.catalog = catalog;
        this.favorites = favorites ?? new FavoriteCatalog(new MemoryFavoriteStore());
        var creators = catalog.Where(x => x.CreatorId is not null)
            .GroupBy(x => x.CreatorId!, StringComparer.Ordinal)
            .Select(x => (Id: x.Key, Name: x.First().Creator))
            .OrderBy(x => x.Name, StringComparer.OrdinalIgnoreCase)
            .ThenBy(x => x.Id, StringComparer.Ordinal).ToArray();
        creatorIds = new string?[] { null }.Concat(creators.Select(x => (string?)x.Id)).ToArray();
        CreatorLabels = new[] { UiText.AllCreators }.Concat(creators.Select(x => x.Name)).ToArray();
        Detail = new DetailViewModel(catalog);
        Items = CatalogSearch.Find(catalog, null);
        OpenDetails = new AsyncCommand(
            async () =>
            {
                NavigationError = "";
                var id = SelectedItem!.Id;
                await navigation.OpenItemAsync(id);
            },
            () => SelectedItem is not null,
            _ => NavigationError = UiText.NavigationError);
        ToggleFavorite = new AsyncCommand(() =>
        {
            this.favorites.Toggle(SelectedItem!.Id);
            FavoritesError = "";
            Rebuild();
            return Task.CompletedTask;
        }, () => this.favorites.IsLoaded && SelectedItem is not null,
            _ => FavoritesError = UiText.FavoritesError);
        RetryFavorites = new AsyncCommand(() =>
        {
            ReloadFavorites();
            return Task.CompletedTask;
        }, () => true, _ => FavoritesError = UiText.FavoritesError);
        ToggleFilters = new AsyncCommand(() =>
        {
            FiltersExpanded = !FiltersExpanded;
            return Task.CompletedTask;
        }, () => true, _ => { });
        ReloadFavorites();
    }

    public IReadOnlyList<string> FilterLabels { get; } =
        [UiText.All, UiText.Books, UiText.Songs, UiText.Podcasts];
    public IReadOnlyList<string> CreatorLabels { get; }
    public IReadOnlyList<string> SortLabels { get; } = [UiText.ByTitle, UiText.NewestFirst];
    public AsyncCommand ToggleFavorite { get; }
    public AsyncCommand RetryFavorites { get; }
    public AsyncCommand ToggleFilters { get; }
    public string FavoriteAction => SelectedItem is { } item && favorites.Contains(item.Id)
        ? UiText.RemoveFavorite : UiText.AddFavorite;
    public bool FiltersExpanded
    {
        get => filtersExpanded;
        set { if (filtersExpanded == value) return; filtersExpanded = value; Notify(); }
    }
    public bool FavoritesOnly
    {
        get => favoritesOnly;
        set { if (favoritesOnly == value) return; favoritesOnly = value; Notify(); Rebuild(); }
    }
    public int CreatorIndex
    {
        get => creatorIndex;
        set
        {
            if (value == -1) value = 0;
            if (value < 0 || value >= creatorIds.Length)
                throw new ArgumentOutOfRangeException(nameof(value));
            if (creatorIndex == value) return;
            creatorIndex = value; Notify(); Rebuild();
        }
    }
    public int SortIndex
    {
        get => sortIndex;
        set
        {
            if (value == -1) value = 0;
            if (value is < 0 or > 1) throw new ArgumentOutOfRangeException(nameof(value));
            if (sortIndex == value) return;
            sortIndex = value; Notify(); Rebuild();
        }
    }
    public string FavoritesError
    {
        get => favoritesError;
        private set { favoritesError = value; Notify(); }
    }
    public string SearchError
    {
        get => searchError;
        private set { searchError = value; Notify(); }
    }
    public void ReportSearchFailure() => SearchError = UiText.SearchError;

    private void ReloadFavorites()
    {
        try { favorites.Reload(); FavoritesError = ""; }
        catch (Exception) { FavoritesError = UiText.FavoritesError; }
        Rebuild();
    }

    public DetailViewModel Detail { get; }
    public AsyncCommand OpenDetails { get; }
    public IReadOnlyList<CatalogItem> Items { get; private set; }
    public string ResultSummary => string.Format(
        CultureInfo.CurrentCulture,
        Items.Count == 1 ? UiText.OneResult : UiText.ResultsFormat, Items.Count);

    public string Query
    {
        get => query;
        set
        {
            value ??= "";
            if (query == value) return;
            query = value;
            SearchError = "";
            Notify();
            Rebuild();
        }
    }

    public int FilterIndex
    {
        get => filterIndex;
        set
        {
            // A Picker can report -1 while selection is being initialized.
            if (value == -1) value = 0;
            if (value is < 0 or > 3)
                throw new ArgumentOutOfRangeException(nameof(value));
            if (filterIndex == value) return;
            filterIndex = value;
            Notify();
            Rebuild();
        }
    }

    public CatalogItem? SelectedItem
    {
        get => selectedItem;
        set
        {
            var match = value is null ? null
                : Items.FirstOrDefault(x => x.Id == value.Id);
            if (ReferenceEquals(selectedItem, match)) return;
            selectedItem = match;
            if (match is null) Detail.Clear(); else Detail.Load(match.Id);
            Notify();
            OpenDetails.Refresh();
            ToggleFavorite.Refresh();
            Notify(nameof(FavoriteAction));
        }
    }

    public string NavigationError
    {
        get => navigationError;
        private set
        {
            if (navigationError == value) return;
            navigationError = value;
            Notify();
        }
    }

    private void Rebuild()
    {
        ContentKind? kind = filterIndex == 0 ? null
            : (ContentKind)(filterIndex - 1);
        Items = CatalogSearch.Find(catalog, query, kind,
            creatorIds[creatorIndex], (CatalogSort)sortIndex,
            favoritesOnly ? favorites.Snapshot() : null);
        Notify(nameof(Items));
        Notify(nameof(ResultSummary));
        SelectedItem = selectedItem;
        NavigationError = "";
        ToggleFavorite.Refresh();
        Notify(nameof(FavoriteAction));
    }
}

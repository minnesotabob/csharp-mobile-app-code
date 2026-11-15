using System.Globalization;
using GnazzosWorld.Core.Catalog;
using GnazzosWorld.Core.Localization;

namespace GnazzosWorld.Core.Presentation;

public sealed class CatalogBrowserViewModel : ObservableObject
{
    private readonly IReadOnlyList<CatalogItem> catalog;
    private string query = "";
    private int filterIndex;
    private CatalogItem? selectedItem;
    private string navigationError = "";

    public CatalogBrowserViewModel(
        IReadOnlyList<CatalogItem> catalog, ICatalogNavigation navigation)
    {
        this.catalog = catalog;
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
    }

    public IReadOnlyList<string> FilterLabels { get; } =
        [UiText.All, UiText.Books, UiText.Songs, UiText.Podcasts];
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
        Items = CatalogSearch.Find(catalog, query, kind);
        Notify(nameof(Items));
        Notify(nameof(ResultSummary));
        SelectedItem = selectedItem;
        NavigationError = "";
    }
}

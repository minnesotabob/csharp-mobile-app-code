using GnazzosWorld.Core.Catalog;

namespace GnazzosWorld.App;

// First-screen scaffold. Later chapters move interaction state into a view model.
public partial class MainPage : ContentPage
{
    private readonly IReadOnlyList<CatalogItem> catalog;
    public MainPage(IReadOnlyList<CatalogItem> catalog)
    {
        InitializeComponent();
        this.catalog = catalog;
        CatalogView.ItemsSource = CatalogSearch.Find(catalog, null);
    }
    private void OnSearchChanged(object? sender, TextChangedEventArgs e)
        => CatalogView.ItemsSource = CatalogSearch.Find(catalog, e.NewTextValue);
}

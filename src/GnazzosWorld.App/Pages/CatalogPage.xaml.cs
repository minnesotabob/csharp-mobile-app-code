using GnazzosWorld.Core.Presentation;

namespace GnazzosWorld.App.Pages;

public partial class CatalogPage : ContentPage
{
    public CatalogPage(CatalogBrowserViewModel viewModel)
    {
        InitializeComponent();
        BindingContext = viewModel;
    }

    private void OnSizeChanged(object? sender, EventArgs e)
    {
        if (ContentGrid is null || DetailPane is null) return;
        var wide = AdaptiveLayout.IsWide(Width);
        ContentGrid.ColumnDefinitions[0].Width =
            new GridLength(wide ? 2 : 1, GridUnitType.Star);
        ContentGrid.ColumnDefinitions[1].Width =
            new GridLength(wide ? 3 : 0, GridUnitType.Star);
        ContentGrid.ColumnSpacing = wide ? 20 : 0;
        DetailPane.IsVisible = wide;
    }
}

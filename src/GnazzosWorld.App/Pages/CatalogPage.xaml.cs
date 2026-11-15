using GnazzosWorld.Core.Presentation;

namespace GnazzosWorld.App.Pages;

public partial class CatalogPage : ContentPage
{
    private readonly DebouncedQuery queryInput = new();
    private string pendingQuery = "";
    public CatalogPage(CatalogBrowserViewModel viewModel)
    {
        InitializeComponent();
        BindingContext = viewModel;
    }

    private async void OnQueryTextChanged(object? sender, TextChangedEventArgs e)
    {
        pendingQuery = e.NewTextValue ?? "";
        if (BindingContext is not CatalogBrowserViewModel vm) return;
        await queryInput.UpdateAsync(pendingQuery,
            text => vm.Query = text, vm.ReportSearchFailure);
    }

    protected override void OnDisappearing()
    {
        queryInput.Cancel();
        // Preserve the last typed query before navigation cancels its delay.
        if (BindingContext is CatalogBrowserViewModel vm) vm.Query = pendingQuery;
        base.OnDisappearing();
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

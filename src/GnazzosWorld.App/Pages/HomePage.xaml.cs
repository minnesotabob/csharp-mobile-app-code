using GnazzosWorld.Core.Localization;

namespace GnazzosWorld.App.Pages;

public partial class HomePage : ContentPage
{
    public HomePage() => InitializeComponent();
    private async void OnAccount(object? sender, EventArgs e)
    {
        try { await Shell.Current.GoToAsync("account"); }
        catch (Exception)
        { await DisplayAlertAsync(UiText.Error, UiText.NavigationError, UiText.Ok); }
    }
    private async void OnNetworkingLab(object? sender, EventArgs e)
    {
        try { await Shell.Current.GoToAsync("networking-lab"); }
        catch (Exception)
        { await DisplayAlertAsync(UiText.Error, UiText.NavigationError, UiText.Ok); }
    }
    private async void OnRequest(object? sender, EventArgs e)
    {
        try { await Shell.Current.GoToAsync("song-request"); }
        catch (Exception)
        { await DisplayAlertAsync(UiText.Error, UiText.NavigationError, UiText.Ok); }
    }
    private async void OnBrowse(object? sender, EventArgs e)
    {
        try { await Shell.Current.GoToAsync("//main/discover/catalog"); }
        catch (Exception)
        {
            await DisplayAlertAsync(UiText.Error, UiText.NavigationError, UiText.Ok);
        }
    }
}

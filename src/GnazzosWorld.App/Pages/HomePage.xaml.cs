using GnazzosWorld.Core.Localization;

namespace GnazzosWorld.App.Pages;

public partial class HomePage : ContentPage
{
    public HomePage() => InitializeComponent();
    private async void OnBackgroundAudioLab(object? sender, EventArgs e)
    {
        try { await Shell.Current.GoToAsync("background-audio-lab"); }
        catch (Exception)
        { await DisplayAlertAsync(UiText.Error, UiText.NavigationError, UiText.Ok); }
    }
    private async void OnAudioLab(object? sender, EventArgs e)
    {
        try { await Shell.Current.GoToAsync("audio-lab"); }
        catch (Exception)
        { await DisplayAlertAsync(UiText.Error, UiText.NavigationError, UiText.Ok); }
    }
    private async void OnSessionLab(object? sender, EventArgs e)
    {
        try { await Shell.Current.GoToAsync("session-lab"); }
        catch (Exception)
        { await DisplayAlertAsync(UiText.Error, UiText.NavigationError, UiText.Ok); }
    }
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
    private async void OnDownloadClicked(object? sender, EventArgs e) => await Shell.Current.GoToAsync("download-lab");
    private async void OnNotificationClicked(object? sender, EventArgs e) => await Shell.Current.GoToAsync("notification-lab");
    private async void OnCapabilityClicked(object? sender, EventArgs e) => await Shell.Current.GoToAsync("capability-lab");
    private async void OnOrderClicked(object? sender, EventArgs e) => await Shell.Current.GoToAsync("order-lab");
    private async void OnOrderTrackingLabClicked(object? sender, EventArgs e) => await Shell.Current.GoToAsync("order-tracking-lab");
    private async void OnDiagnosticsLabClicked(object? sender, EventArgs e) => await Shell.Current.GoToAsync("diagnostics-lab");
}

using GnazzosWorld.App.Pages;
using GnazzosWorld.Core.Localization;

namespace GnazzosWorld.App;

public sealed class AppShell : Shell
{
    public AppShell(HomePage home, CatalogPage catalog)
    {
        FlyoutBehavior = FlyoutBehavior.Disabled;
        Routing.RegisterRoute("details", typeof(DetailsPage));
        Routing.RegisterRoute("song-request", typeof(SongRequestPage));
        Routing.RegisterRoute("networking-lab", typeof(NetworkingLabPage));
        Routing.RegisterRoute("account", typeof(AccountPage));
        Routing.RegisterRoute("session-lab", typeof(SessionLabPage));
        Routing.RegisterRoute("audio-lab", typeof(AudioLabPage));
        Routing.RegisterRoute("background-audio-lab", typeof(BackgroundAudioLabPage));
        var tabs = new TabBar { Route = "main" };
        var homeTab = new Tab { Route = "home", Title = UiText.Home };
        homeTab.Items.Add(new ShellContent
            { Route = "welcome", Title = UiText.Home, Content = home });
        var discoverTab = new Tab
            { Route = "discover", Title = UiText.Discover };
        discoverTab.Items.Add(new ShellContent
            { Route = "catalog", Title = UiText.Discover, Content = catalog });
        tabs.Items.Add(homeTab);
        tabs.Items.Add(discoverTab);
        Items.Add(tabs);
    }
}

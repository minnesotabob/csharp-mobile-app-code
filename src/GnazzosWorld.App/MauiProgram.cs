using GnazzosWorld.App.Navigation;
using GnazzosWorld.App.Pages;
using GnazzosWorld.App.Storage;
using GnazzosWorld.Core.Catalog;
using GnazzosWorld.Core.Presentation;
using GnazzosWorld.Core.Storage;
using Microsoft.Extensions.DependencyInjection;

namespace GnazzosWorld.App;

public static class MauiProgram
{
    public static MauiApp CreateMauiApp()
    {
        var builder = MauiApp.CreateBuilder().UseMauiApp<App>();
        builder.Services.AddSingleton<IReadOnlyList<CatalogItem>>(
            _ => CatalogLoader.LoadBundled());
        builder.Services.AddSingleton<IFavoriteStore, PreferencesFavoriteStore>();
        builder.Services.AddSingleton<FavoriteCatalog>();
        // Chapter 19: registered for the storage lab, not yet a replacement for
        // the existing JSON draft store or bundled catalog on the main screen.
        builder.Services.AddSingleton(_ => new LocalCatalogDatabase(
            Path.Combine(FileSystem.Current.AppDataDirectory, "public-catalog-v2.sqlite3")));
        builder.Services.AddSingleton<ICatalogNavigation, ShellCatalogNavigation>();
        builder.Services.AddSingleton<IExternalBrowser, ExternalBrowser>();
        builder.Services.AddTransient<CatalogBrowserViewModel>();
        builder.Services.AddTransient<DetailViewModel>();
        builder.Services.AddSingleton<GnazzosWorld.Core.Orders.IRequestDraftStore, FileRequestDraftStore>();
        builder.Services.AddSingleton<SongRequestViewModel>();
        builder.Services.AddSingleton(TimeProvider.System);
        builder.Services.AddSingleton<GnazzosWorld.Core.Orders.ISongSubmissionService,
            GnazzosWorld.Core.Orders.DemoSongSubmissionService>();
        builder.Services.AddSingleton<SongSubmissionViewModel>();
        builder.Services.AddTransient<SongRequestPage>();
        builder.Services.AddSingleton<GnazzosWorld.Core.Networking.DemoCatalogSource>();
        builder.Services.AddTransient<NetworkingLabPage>();
        builder.Services.AddTransient<HomePage>();
        builder.Services.AddTransient<CatalogPage>();
        builder.Services.AddTransient<DetailsPage>();
        builder.Services.AddTransient<AppShell>();
        return builder.Build();
    }
}

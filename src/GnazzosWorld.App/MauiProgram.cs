using GnazzosWorld.App.Navigation;
using GnazzosWorld.App.Pages;
using GnazzosWorld.Core.Catalog;
using GnazzosWorld.Core.Presentation;
using Microsoft.Extensions.DependencyInjection;

namespace GnazzosWorld.App;

public static class MauiProgram
{
    public static MauiApp CreateMauiApp()
    {
        var builder = MauiApp.CreateBuilder().UseMauiApp<App>();
        builder.Services.AddSingleton<IReadOnlyList<CatalogItem>>(
            _ => CatalogLoader.LoadBundled());
        builder.Services.AddSingleton<ICatalogNavigation, ShellCatalogNavigation>();
        builder.Services.AddTransient<CatalogBrowserViewModel>();
        builder.Services.AddTransient<DetailViewModel>();
        builder.Services.AddTransient<HomePage>();
        builder.Services.AddTransient<CatalogPage>();
        builder.Services.AddTransient<DetailsPage>();
        builder.Services.AddTransient<AppShell>();
        return builder.Build();
    }
}

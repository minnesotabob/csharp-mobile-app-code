using GnazzosWorld.Core.Catalog;
using Microsoft.Extensions.DependencyInjection;

namespace GnazzosWorld.App;

public static class MauiProgram
{
    public static MauiApp CreateMauiApp()
    {
        var builder = MauiApp.CreateBuilder().UseMauiApp<App>();
        builder.Services.AddSingleton<IReadOnlyList<CatalogItem>>(
            _ => CatalogLoader.LoadBundled());
        builder.Services.AddTransient<MainPage>();
        return builder.Build();
    }
}

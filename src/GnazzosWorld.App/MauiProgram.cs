using GnazzosWorld.App.Identity;
using GnazzosWorld.Core.Networking;
using GnazzosWorld.Core.Identity;
using GnazzosWorld.App.Navigation;
using GnazzosWorld.App.Pages;
using GnazzosWorld.App.Storage;
using GnazzosWorld.Core.Catalog;
using GnazzosWorld.Core.Presentation;
using GnazzosWorld.Core.Storage;
using Microsoft.Extensions.DependencyInjection;
using CommunityToolkit.Maui;
using GnazzosWorld.Core.Media;
using GnazzosWorld.App.Media;

namespace GnazzosWorld.App;

public static class MauiProgram
{
    public static MauiApp CreateMauiApp()
    {
        var builder = MauiApp.CreateBuilder().UseMauiApp<App>()
            .UseMauiCommunityToolkitMediaElement(isAndroidForegroundServiceEnabled: true);
        builder.ConfigureMauiHandlers(handlers =>
            handlers.AddHandler<BackgroundMediaElement, FocusAwareMediaElementHandler>());
        builder.Services.AddSingleton(_ => new AudioProgressDatabase(
            Path.Combine(FileSystem.Current.AppDataDirectory, "anonymous-audio-progress.sqlite3")));
        builder.Services.AddTransient<AudioLabPage>();
        builder.Services.AddTransient<BackgroundAudioLabPage>();
        builder.Services.AddSingleton(_ => new GnazzosWorld.Core.Downloads.PublicMediaDownloader(
            Path.Combine(FileSystem.Current.CacheDirectory, "public-media-v1")));
        builder.Services.AddTransient<DownloadLabPage>();
        builder.Services.AddSingleton(_ => new GnazzosWorld.Core.Notifications.RouteInbox(
            Path.Combine(FileSystem.Current.AppDataDirectory, "untrusted-route-hints.sqlite3")));
        builder.Services.AddSingleton<GnazzosWorld.App.Notifications.NotificationRouter>();
        builder.Services.AddSingleton<GnazzosWorld.App.Notifications.LocalNotificationService>();
        builder.Services.AddTransient<NotificationLabPage>();
        builder.Services.AddSingleton(sp => new GnazzosWorld.Core.Attachments.AttachmentExportStore(
            Path.Combine(FileSystem.Current.CacheDirectory, "sharing-root"), sp.GetRequiredService<TimeProvider>()));
        builder.Services.AddTransient<CapabilityLabPage>();
        builder.Services.AddSingleton(_=>new GnazzosWorld.Core.Commerce.PendingOrderDatabase(
            Path.Combine(FileSystem.Current.AppDataDirectory,"synthetic-order-operations.sqlite3")));
        builder.Services.AddSingleton<GnazzosWorld.Core.Commerce.OrderApiClient>();
        builder.Services.AddSingleton<GnazzosWorld.Core.Commerce.OrderAttemptCoordinator>();
        builder.Services.AddTransient<OrderLabPage>();
        builder.Services.AddSingleton(_=>new GnazzosWorld.Core.Fulfillment.PendingRevisionDatabase(
            Path.Combine(FileSystem.Current.AppDataDirectory,"synthetic-revision-operations-v1.sqlite3")));
        builder.Services.AddSingleton<GnazzosWorld.Core.Fulfillment.TrackingApiClient>();
        builder.Services.AddSingleton<GnazzosWorld.Core.Fulfillment.RevisionCoordinator>();
        builder.Services.AddTransient<OrderTrackingLabPage>();
        builder.Services.AddSingleton(sp=>new GnazzosWorld.Core.Operations.SafeDiagnosticBuffer(
            sp.GetRequiredService<TimeProvider>(),Preferences.Default.Get("safe-diagnostic-consent-v1",false)));
        builder.Services.AddTransient<DiagnosticsLabPage>();
        builder.Services.AddTransient<PerformanceLabPage>();
        builder.Services.AddTransient<LinkedRequestPage>();
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
        builder.Services.AddSingleton<IInteractiveSignIn, Auth0InteractiveSignIn>();
        builder.Services.AddSingleton<SignInSession>();
        builder.Services.AddTransient<AccountPage>();
        builder.Services.AddSingleton<ISessionVault, MauiSessionVault>();
        builder.Services.AddSingleton<ISessionProvider, Auth0SessionProvider>();
        builder.Services.AddSingleton(sp => new ApiSession(AuthenticationConfiguration.ApiSession,
            sp.GetRequiredService<ISessionVault>(), sp.GetRequiredService<ISessionProvider>(), TimeProvider.System));
        builder.Services.AddSingleton(sp => new ProtectedRequestClient(
            new HttpClient(new HttpClientHandler { AllowAutoRedirect = false }) { Timeout = TimeSpan.FromSeconds(20) },
            sp.GetRequiredService<ApiSession>()));
        builder.Services.AddTransient<SessionLabPage>();
        builder.Services.AddTransient<HomePage>();
        builder.Services.AddTransient<CatalogPage>();
        builder.Services.AddTransient<DetailsPage>();
        builder.Services.AddTransient<AppShell>();
        return builder.Build();
    }
}

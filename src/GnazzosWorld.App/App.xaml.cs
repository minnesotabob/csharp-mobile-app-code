using Microsoft.Extensions.DependencyInjection;

namespace GnazzosWorld.App;

public partial class App : Application
{
    private readonly IServiceProvider services;
    public App(IServiceProvider services)
    {
        InitializeComponent();
        this.services = services;
    }
    protected override Window CreateWindow(IActivationState? activationState)
    {
        var shell = services.GetRequiredService<AppShell>(); var window = new Window(shell);
        var router = services.GetRequiredService<GnazzosWorld.App.Notifications.NotificationRouter>();
        shell.Loaded += (_, _) => router.Ready(window);
        window.Activated += (_, _) => router.Ready(window);
        window.Resumed += (_, _) => router.Ready(window);
        window.Stopped += (_, _) => router.Background();
        window.Destroying += (_, _) => router.Background();
        return window;
    }
}

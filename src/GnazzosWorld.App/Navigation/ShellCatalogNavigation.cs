using GnazzosWorld.Core.Presentation;

namespace GnazzosWorld.App.Navigation;

public sealed class ShellCatalogNavigation : ICatalogNavigation
{
    public Task OpenItemAsync(string id)
    {
        if (!RoutePayload.IsValidId(id))
            throw new ArgumentException("Invalid catalog ID.", nameof(id));
        var shell = Shell.Current
            ?? throw new InvalidOperationException("The shell is not ready.");
        return shell.GoToAsync("details",
            new ShellNavigationQueryParameters { ["id"] = id });
    }
}

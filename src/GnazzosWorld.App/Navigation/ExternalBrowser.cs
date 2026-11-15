using GnazzosWorld.Core.Catalog;
using GnazzosWorld.Core.Presentation;

namespace GnazzosWorld.App.Navigation;

public sealed class ExternalBrowser : IExternalBrowser
{
    public Task<bool> OpenAsync(Uri uri)
    {
        if (!ContentLinks.IsAllowed(uri))
            throw new ArgumentException("Unsupported content destination.", nameof(uri));
        return Browser.Default.OpenAsync(uri, BrowserLaunchMode.External);
    }
}

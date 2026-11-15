namespace GnazzosWorld.Core.Presentation;

public interface IExternalBrowser
{
    // Success means handoff, not that a remote page loaded or media played.
    Task<bool> OpenAsync(Uri uri);
}

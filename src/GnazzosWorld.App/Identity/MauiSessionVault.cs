using GnazzosWorld.Core.Identity;

namespace GnazzosWorld.App.Identity;

public sealed class MauiSessionVault : ISessionVault
{
    private const string Key = "gnazzosworld-api-session-v1";
    private const string Installation = "gnazzosworld-session-install-v1";
    private async Task InitializeAsync()
    {
        // iOS Keychain can outlive an installation. Set the nonsecret flag only
        // after overwriting this app's credential entry successfully.
        if (!Preferences.Default.ContainsKey(Installation))
        {
            await SecureStorage.Default.SetAsync(Key, SessionEnvelopeCodec.SignedOut);
            Preferences.Default.Set(Installation, true);
        }
    }
    public async Task<SessionGrant?> ReadAsync(CancellationToken ct)
    {
        ct.ThrowIfCancellationRequested(); await InitializeAsync();
        var text = await SecureStorage.Default.GetAsync(Key);
        ct.ThrowIfCancellationRequested(); return SessionEnvelopeCodec.Decode(text);
    }
    public async Task WriteAsync(SessionGrant? grant, CancellationToken ct)
    {
        ct.ThrowIfCancellationRequested(); await InitializeAsync();
        await SecureStorage.Default.SetAsync(Key, SessionEnvelopeCodec.Encode(grant));
        ct.ThrowIfCancellationRequested();
    }
}

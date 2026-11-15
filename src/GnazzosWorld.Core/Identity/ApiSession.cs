namespace GnazzosWorld.Core.Identity;

/// <summary>One coordinator per vault. Serializes rotation and fences obsolete completions.</summary>
public sealed class ApiSession(ApiSessionSettings? settings, ISessionVault vault,
    ISessionProvider provider, TimeProvider clock)
{
    private readonly SemaphoreSlim gate = new(1, 1);
    private SessionGrant? grant;
    private bool loaded;
    private int blocked;
    private long generation;
    public ApiSessionSettings Settings => settings ?? throw new SessionUnconfiguredException();
    public bool IsCurrent(AccessLease lease) => Volatile.Read(ref blocked) == 0 && lease.Generation == Interlocked.Read(ref generation);
    private void Validate(SessionGrant value, bool newlyIssued)
    {
        SessionEnvelopeCodec.ValidateShape(value);
        if (value.Issuer != Settings.Identity.Issuer || value.Audience != Settings.Audience ||
            (newlyIssued && value.ExpiresUtc <= clock.GetUtcNow()))
            throw new InvalidDataException("Session does not match this client and resource.");
    }
    private async Task PersistAsync(SessionGrant? value, CancellationToken ct)
    {
        try { await vault.WriteAsync(value, ct); }
        catch { grant = null; Volatile.Write(ref blocked, 1); throw new SessionStorageException(); }
    }
    private void Check(long attempt)
    {
        if (attempt != Interlocked.Read(ref generation) || Volatile.Read(ref blocked) != 0)
            throw new SessionEndedException();
    }
    public async Task SignInAsync(CancellationToken ct = default)
    {
        _ = Settings;
        await gate.WaitAsync(ct);
        try
        {
            if (Volatile.Read(ref blocked) != 0) throw new SessionStorageException();
            if (grant is not null) throw new InvalidOperationException("Sign out locally before replacing this session.");
            // Clear a previous installation/session before attempting an account replacement.
            var attempt = Interlocked.Increment(ref generation);
            await PersistAsync(null, ct); loaded = true;
            var result = await provider.SignInAsync(ct);
            ct.ThrowIfCancellationRequested(); Check(attempt); Validate(result, true);
            await PersistAsync(result, ct); Check(attempt);
            grant = result;
        }
        finally { gate.Release(); }
    }
    public async Task<AccessLease> AcquireAsync(CancellationToken ct = default)
    {
        _ = Settings;
        await gate.WaitAsync(ct);
        try
        {
            if (Volatile.Read(ref blocked) != 0) throw new SessionStorageException();
            var attempt = Interlocked.Read(ref generation);
            if (!loaded)
            {
                try { grant = await vault.ReadAsync(ct); if (grant is not null) Validate(grant, false); loaded = true; }
                catch (OperationCanceledException) when (ct.IsCancellationRequested) { throw; }
                catch { grant = null; Volatile.Write(ref blocked, 1); throw new SessionStorageException(); }
            }
            Check(attempt);
            if (grant is null) throw new InteractiveSignInRequiredException();
            if (grant.ExpiresUtc <= clock.GetUtcNow().AddSeconds(60))
            {
                var previous = grant;
                if (previous.RefreshToken is null)
                {
                    grant = null; await PersistAsync(null, ct); throw new InteractiveSignInRequiredException();
                }
                SessionGrant next;
                try
                {
                    next = await provider.RefreshAsync(previous, ct);
                    ct.ThrowIfCancellationRequested(); Check(attempt); Validate(next, true);
                    if (next.Subject != previous.Subject) throw new InvalidDataException("Refresh changed the subject.");
                }
                catch (Exception) when (attempt == Interlocked.Read(ref generation))
                {
                    // A rotated refresh may have been consumed even if its reply was lost.
                    // No automatic replay of an uncertain credential; require interactive recovery.
                    grant = null; await PersistAsync(null, CancellationToken.None);
                    Interlocked.Increment(ref generation);
                    throw new InteractiveSignInRequiredException();
                }
                await PersistAsync(next, ct); Check(attempt); grant = next;
            }
            Check(attempt);
            return new(grant.AccessToken, attempt);
        }
        finally { gate.Release(); }
    }
    public async Task SignOutLocalAsync(CancellationToken ct = default)
    {
        Interlocked.Increment(ref generation);
        Volatile.Write(ref blocked, 1); // Invalidate leases before waiting for an in-flight refresh.
        await gate.WaitAsync(ct);
        try
        {
            grant = null; loaded = true;
            await PersistAsync(null, ct);
            Volatile.Write(ref blocked, 0);
        }
        finally { gate.Release(); }
    }
    public async Task RejectLeaseAsync(AccessLease lease, CancellationToken ct = default)
    {
        await gate.WaitAsync(ct);
        try
        {
            if (!IsCurrent(lease)) return;
            Interlocked.Increment(ref generation); grant = null; loaded = true;
            await PersistAsync(null, ct);
        }
        finally { gate.Release(); }
    }
}

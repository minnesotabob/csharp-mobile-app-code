namespace GnazzosWorld.Core.Notifications;

public enum PushPermission { Unknown, Granted, Denied }
public sealed record PushBinding(string InstallationId, string Provider, string Token, long Generation)
{
    public override string ToString() => "PushBinding [device token redacted]";
    public bool IsValid() => Guid.TryParseExact(InstallationId, "N", out var parsed) && parsed.ToString("N") == InstallationId && Provider is "fcm" or "apns" &&
        !string.IsNullOrWhiteSpace(Token) && Token.Length <= 2048 && Token.All(c => c is >= '!' and <= '~') && Generation is > 0 and <= 1000000000;
}
public interface IPushRegistrationSink
{
    // The sink must use current authenticated owner credentials, never an owner from a notification.
    Task UpsertAsync(PushBinding binding, CancellationToken ct);
    Task RemoveAsync(string installationId, CancellationToken ct);
}

/// <summary>Serialized optional token lifecycle; real provider acquisition and authenticated sink composition are separate gates.</summary>
public sealed class PushRegistrationCoordinator(IPushRegistrationSink sink, string installationId, long initialGeneration, Action<long> persistGeneration)
{
    private readonly SemaphoreSlim gate = new(1, 1);
    private long generation = initialGeneration;
    public bool RegistrationConfirmed { get; private set; }
    public async Task UpdateAsync(PushPermission permission, string? provider, string? token, CancellationToken ct = default)
    {
        await gate.WaitAsync(ct);
        try
        {
            RegistrationConfirmed = false;
            if (permission != PushPermission.Granted || token is null)
            { await sink.RemoveAsync(installationId, ct); return; }
            var next = checked(generation + 1);
            var binding = new PushBinding(installationId, provider ?? "", token, next);
            if (!binding.IsValid()) throw new InvalidDataException("Invalid push registration shape.");
            persistGeneration(next); generation = next; // Persist before send; a lost reply must not reuse an older generation.
            await sink.UpsertAsync(binding, ct); ct.ThrowIfCancellationRequested(); RegistrationConfirmed = true;
        }
        finally { gate.Release(); }
    }
    public Task DisableAsync(CancellationToken ct = default) => UpdateAsync(PushPermission.Denied, null, null, ct);
}

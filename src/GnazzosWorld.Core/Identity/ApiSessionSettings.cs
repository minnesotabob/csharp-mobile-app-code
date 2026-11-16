namespace GnazzosWorld.Core.Identity;

public sealed class ApiSessionSettings
{
    public NativeIdentitySettings Identity { get; }
    public string Audience { get; }
    public Uri ApiOrigin { get; }
    public ApiSessionSettings(NativeIdentitySettings identity, string audience, Uri apiOrigin)
    {
        ArgumentNullException.ThrowIfNull(identity);
        if (!Uri.TryCreate(audience, UriKind.Absolute, out var resource) || resource.Scheme != "https" ||
            resource.UserInfo.Length != 0 || resource.Query.Length != 0 || resource.Fragment.Length != 0 || audience.Length > 256)
            throw new ArgumentException("Use the configured HTTPS API resource identifier.", nameof(audience));
        if (!apiOrigin.IsAbsoluteUri || apiOrigin.Scheme != "https" || apiOrigin.UserInfo.Length != 0 ||
            apiOrigin.AbsolutePath != "/" || apiOrigin.Query.Length != 0 || apiOrigin.Fragment.Length != 0)
            throw new ArgumentException("Use one HTTPS API origin without a path or query.", nameof(apiOrigin));
        Identity = identity; Audience = audience; ApiOrigin = apiOrigin;
    }
}
public sealed class SessionGrant(Uri issuer, string subject, string audience, string accessToken,
    string? refreshToken, DateTimeOffset expiresUtc)
{
    public Uri Issuer { get; } = issuer;
    public string Subject { get; } = subject;
    public string Audience { get; } = audience;
    public string AccessToken { get; } = accessToken;
    public string? RefreshToken { get; } = refreshToken;
    public DateTimeOffset ExpiresUtc { get; } = expiresUtc;
    public override string ToString() => "SessionGrant [credentials redacted]";
}
public interface ISessionVault
{
    Task<SessionGrant?> ReadAsync(CancellationToken ct);
    Task WriteAsync(SessionGrant? grant, CancellationToken ct);
}
public interface ISessionProvider
{
    Task<SessionGrant> SignInAsync(CancellationToken ct);
    Task<SessionGrant> RefreshAsync(SessionGrant previous, CancellationToken ct);
}
public sealed class InteractiveSignInRequiredException : Exception
{
    public InteractiveSignInRequiredException() : base("Sign in again to continue.") { }
}
public sealed class SessionStorageException : Exception
{
    public SessionStorageException() : base("Session storage needs attention. Retry local sign-out before signing in.") { }
}
public sealed class SessionEndedException : Exception
{
    public SessionEndedException() : base("This operation belongs to an earlier local session.") { }
}
public sealed class SessionUnconfiguredException : Exception
{
    public SessionUnconfiguredException() : base("The API session is not configured in this teaching build.") { }
}
public readonly record struct AccessLease(string Token, long Generation)
{
    public override string ToString() => "AccessLease [credential redacted]";
}

public sealed record OwnedAccessLease(AccessLease Access, Uri Issuer, string Subject)
{
    public override string ToString() => "Owned access lease [identity and credential omitted]";
}

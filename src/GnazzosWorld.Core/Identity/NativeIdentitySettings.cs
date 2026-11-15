namespace GnazzosWorld.Core.Identity;

/// <summary>Public client settings, not credentials. This lab supports standard Auth0 tenant domains.</summary>
public sealed class NativeIdentitySettings
{
    public const string CallbackScheme = "com.johngnazzo.gnazzosworld";
    public const string Callback = CallbackScheme + "://callback/";
    public string Domain { get; }
    public string ClientId { get; }
    public Uri Issuer { get; }
    public NativeIdentitySettings(string domain, string clientId)
    {
        if (string.IsNullOrWhiteSpace(domain) || domain.Length > 253 ||
            domain != domain.ToLowerInvariant() || !domain.EndsWith(".auth0.com", StringComparison.Ordinal) ||
            Uri.CheckHostName(domain) != UriHostNameType.Dns || domain.Any(c => !char.IsAsciiLetterOrDigit(c) && c is not '.' and not '-'))
            throw new ArgumentException("Use the lowercase Auth0 tenant hostname, without a scheme, port or path.", nameof(domain));
        if (string.IsNullOrWhiteSpace(clientId) || clientId.Length is < 8 or > 128 ||
            clientId.Any(c => !char.IsAsciiLetterOrDigit(c) && c is not '_' and not '-'))
            throw new ArgumentException("Use the public native application's client ID.", nameof(clientId));
        Domain = domain; ClientId = clientId; Issuer = new Uri("https://" + domain + "/");
    }
    // Limits what the browser adapter may launch. The SDK still owns OAuth/OIDC validation.
    public void ValidateBrowserRequest(string start, string end)
    {
        if (end != Callback || !Uri.TryCreate(start, UriKind.Absolute, out var uri) ||
            uri.Scheme != Uri.UriSchemeHttps || uri.Host != Domain || uri.Port != 443 ||
            uri.UserInfo.Length != 0 || uri.Fragment.Length != 0 || uri.AbsolutePath != "/authorize")
            throw new InvalidDataException("Unexpected authentication browser destination.");
    }
}

using System.Text;
using System.Text.Json;

namespace GnazzosWorld.Core.Identity;

public static class SessionEnvelopeCodec
{
    private sealed record Envelope(int Schema, string Issuer, string Subject, string Audience,
        string AccessToken, string? RefreshToken, DateTimeOffset ExpiresUtc)
    {
        public override string ToString() => "Stored session [credentials redacted]";
    }
    public const string SignedOut = "signed-out-v1";
    public static string Encode(SessionGrant? grant)
    {
        if (grant is null) return SignedOut;
        ValidateShape(grant);
        var text = JsonSerializer.Serialize(new Envelope(1, grant.Issuer.AbsoluteUri, grant.Subject,
            grant.Audience, grant.AccessToken, grant.RefreshToken, grant.ExpiresUtc));
        if (Encoding.UTF8.GetByteCount(text) > 8192) throw new InvalidDataException("Session envelope is too large.");
        return text;
    }
    public static SessionGrant? Decode(string? text)
    {
        if (text is null || text == SignedOut) return null;
        if (Encoding.UTF8.GetByteCount(text) > 8192) throw new InvalidDataException("Session envelope is too large.");
        var e = JsonSerializer.Deserialize<Envelope>(text) ?? throw new InvalidDataException("Invalid session envelope.");
        if (e.Schema != 1 || !Uri.TryCreate(e.Issuer, UriKind.Absolute, out var issuer))
            throw new InvalidDataException("Unsupported session envelope.");
        var grant = new SessionGrant(issuer, e.Subject, e.Audience, e.AccessToken, e.RefreshToken, e.ExpiresUtc);
        ValidateShape(grant); return grant;
    }
    internal static void ValidateShape(SessionGrant grant)
    {
        if (!grant.Issuer.IsAbsoluteUri || grant.Issuer.Scheme != "https" ||
            string.IsNullOrWhiteSpace(grant.Subject) || grant.Subject.Length > 256 || grant.Subject.Any(char.IsControl) ||
            string.IsNullOrWhiteSpace(grant.Audience) || grant.Audience.Length > 256 ||
            !Token(grant.AccessToken) || (grant.RefreshToken is not null && !Token(grant.RefreshToken)))
            throw new InvalidDataException("Invalid session fields.");
    }
    private static bool Token(string? value) => !string.IsNullOrEmpty(value) && value.Length <= 8192 &&
        value.All(c => c is >= '!' and <= '~');
}

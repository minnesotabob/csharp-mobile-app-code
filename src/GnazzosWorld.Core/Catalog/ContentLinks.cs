namespace GnazzosWorld.Core.Catalog;

// Review this explicit list before adding a new publisher or service.
public static class ContentLinks
{
    public static bool IsAllowed(Uri? uri) => uri is
        { IsAbsoluteUri: true, Scheme: "https", Port: 443 }
        && string.IsNullOrEmpty(uri.UserInfo)
        && (uri.IdnHost.Equals("johngnazzo.com", StringComparison.OrdinalIgnoreCase)
            || uri.IdnHost.Equals("www.johngnazzo.com", StringComparison.OrdinalIgnoreCase));
}

public sealed record ReadingSample(string Heading, string Text, string Credit);

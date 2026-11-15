namespace GnazzosWorld.Core.Presentation;

public static class RoutePayload
{
    public static string? ReadItemId(IDictionary<string, object> query)
    {
        ArgumentNullException.ThrowIfNull(query);
        if (!query.TryGetValue("id", out var value) || value is not string id)
            return null;
        return IsValidId(id) ? id : null;
    }

    public static bool IsValidId(string? id)
        => !string.IsNullOrEmpty(id) && id.Length <= 128
        && id.All(c => char.IsAsciiLetterOrDigit(c) || c is '-' or '_' or '.');
}

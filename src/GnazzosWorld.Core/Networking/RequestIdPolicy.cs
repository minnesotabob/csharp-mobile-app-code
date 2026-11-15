namespace GnazzosWorld.Core.Networking;

/// <summary>A request ID is data, never a URL, filesystem path or account selector.</summary>
public static class RequestIdPolicy
{
    public const int MaximumLength = 64;
    public static bool IsValid(string? id) => !string.IsNullOrEmpty(id) && id.Length <= MaximumLength &&
        id.All(c => char.IsAsciiLetterOrDigit(c) || c is '-' or '_');
}

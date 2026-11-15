using System.Globalization;

namespace GnazzosWorld.Core.Sync;

public enum UserDataKind { Favorite, Bookmark, Progress, Draft }
public sealed record UserDataKey(UserDataKind Kind, string ItemId);
public sealed record UserDataRow(UserDataKey Key, long Version, string? Value);
public sealed record UserEdit(Guid OperationId, UserDataKey Key, long ExpectedVersion, string? Value);
public sealed record EditReply(string Account, bool Accepted, UserDataRow Current);
public sealed record ChangePage(string Account, long After, long Next, IReadOnlyList<UserDataRow> Rows, bool HasMore);
public sealed record ReplicaView(string? Value, string? ServerValue, long ServerVersion, bool Pending, bool Conflict);
public sealed record SyncPass(int Accepted, int Conflicts, int Downloaded, bool MoreRemoteChanges);

// A transport is bound to one context. In this chapter that context is simulated.
public interface IUserSyncTransport
{
    string Account { get; }
    Task<EditReply> PushAsync(UserEdit edit, CancellationToken ct);
    Task<ChangePage> PullAsync(long after, int limit, CancellationToken ct);
}

internal static class SyncRules
{
    internal static void Identity(string value)
    {
        if (string.IsNullOrWhiteSpace(value) || value.Length > 128 || value.Any(char.IsControl))
            throw new ArgumentException("Expected a bounded, nonempty identity.");
    }
    internal static void Value(UserDataKey key, string? value)
    {
        ArgumentNullException.ThrowIfNull(key);
        Identity(key.ItemId);
        if (!Enum.IsDefined(key.Kind)) throw new ArgumentException("Unknown user-data kind.");
        if (value is null) return; // A versioned deletion, including deletion of a draft.
        if (value.Length > 2048 || value.Any(char.IsControl))
            throw new ArgumentException("Invalid demonstration value.");
        switch (key.Kind)
        {
            case UserDataKind.Favorite when value != "saved":
                throw new ArgumentException("A favorite is saved or deleted.");
            case UserDataKind.Progress when !int.TryParse(value, NumberStyles.None,
                CultureInfo.InvariantCulture, out var n) || n is < 0 or > 100:
                throw new ArgumentException("Progress must be an integer from 0 to 100.");
            case UserDataKind.Bookmark when string.IsNullOrWhiteSpace(value) || value.Length > 128:
            case UserDataKind.Draft when string.IsNullOrWhiteSpace(value):
                throw new ArgumentException("Expected a nonempty demonstration value.");
        }
    }
    internal static void Edit(UserEdit edit)
    {
        ArgumentNullException.ThrowIfNull(edit);
        if (edit.OperationId == Guid.Empty || edit.ExpectedVersion < 0)
            throw new ArgumentException("Invalid operation identity or base version.");
        Value(edit.Key, edit.Value);
    }
}

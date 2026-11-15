using System.Text;
using System.Text.Json;

namespace GnazzosWorld.Core.Catalog;

public interface IFavoriteStore
{
    IReadOnlyList<string> Load();
    void Save(IReadOnlyList<string> ids);
}

public static class FavoriteIdCodec
{
    public const int MaximumCount = 32;
    private const int MaximumBytes = 4096;

    public static string[] Validate(IEnumerable<string> ids)
    {
        ArgumentNullException.ThrowIfNull(ids);
        var result = new HashSet<string>(StringComparer.Ordinal);
        foreach (var id in ids)
        {
            if (string.IsNullOrWhiteSpace(id) || id.Length > 128)
                throw new InvalidDataException("Invalid favorite ID.");
            result.Add(id);
            if (result.Count > MaximumCount)
                throw new InvalidDataException("Too many favorites for this teaching store.");
        }
        return result.Order(StringComparer.Ordinal).ToArray();
    }

    public static string Encode(IEnumerable<string> ids)
    {
        var json = JsonSerializer.Serialize(Validate(ids));
        CheckSize(json);
        return json;
    }

    public static IReadOnlyList<string> Decode(string json)
    {
        ArgumentNullException.ThrowIfNull(json);
        CheckSize(json);
        try
        {
            return Validate(JsonSerializer.Deserialize<string[]>(json)
                ?? throw new InvalidDataException("Favorites must be an array."));
        }
        catch (JsonException error)
        {
            throw new InvalidDataException("Favorites could not be read.", error);
        }
    }

    private static void CheckSize(string json)
    {
        if (Encoding.UTF8.GetByteCount(json) > MaximumBytes)
            throw new InvalidDataException("Favorites exceed the teaching storage limit.");
    }
}

public sealed class MemoryFavoriteStore : IFavoriteStore
{
    private string json = "[]";
    public IReadOnlyList<string> Load() => FavoriteIdCodec.Decode(json);
    public void Save(IReadOnlyList<string> ids) => json = FavoriteIdCodec.Encode(ids);
}

// Used on the UI thread. Durable storage succeeds before visible state changes.
public sealed class FavoriteCatalog(IFavoriteStore store)
{
    private HashSet<string> ids = new(StringComparer.Ordinal);
    public bool IsLoaded { get; private set; }
    public IReadOnlySet<string> Snapshot() => new HashSet<string>(ids, StringComparer.Ordinal);
    public bool Contains(string id) => ids.Contains(id);

    public void Reload()
    {
        var next = FavoriteIdCodec.Validate(store.Load());
        ids = new HashSet<string>(next, StringComparer.Ordinal);
        IsLoaded = true;
    }

    public void Toggle(string id)
    {
        if (!IsLoaded) throw new InvalidOperationException("Load favorites before editing.");
        FavoriteIdCodec.Validate([id]);
        var next = new HashSet<string>(ids, StringComparer.Ordinal);
        if (!next.Remove(id)) next.Add(id);
        var ordered = FavoriteIdCodec.Validate(next);
        store.Save(ordered);
        ids = new HashSet<string>(ordered, StringComparer.Ordinal);
    }
}

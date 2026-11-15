using System.Text.Json;
using GnazzosWorld.Core.Orders;

namespace GnazzosWorld.App.Storage;

// Anonymous local draft only. Account isolation and encryption are later work.
public sealed class FileRequestDraftStore : IRequestDraftStore
{
    private string PathName => Path.Combine(
        FileSystem.Current.AppDataDirectory, "song-request-v1.json");
    public SongRequest? Load()
    {
        if (!File.Exists(PathName)) return null;
        var envelope = JsonSerializer.Deserialize<Envelope>(File.ReadAllText(PathName));
        if (envelope is null || envelope.Version != 1 || envelope.Draft is null)
            throw new JsonException("Unsupported or missing draft.");
        var d = envelope.Draft;
        if (d.Genre is null || d.Occasion is null || d.VocalNotes is null ||
            d.Story is null || d.ContactName is null || d.Email is null)
            throw new JsonException("Draft fields cannot be null.");
        return d;
    }
    public void Save(SongRequest draft)
    {
        var temporary = PathName + ".tmp";
        File.WriteAllText(temporary, JsonSerializer.Serialize(new Envelope(1, draft)));
        File.Move(temporary, PathName, overwrite: true);
    }
    public void Delete() => File.Delete(PathName);
    private sealed record Envelope(int Version, SongRequest Draft);
}

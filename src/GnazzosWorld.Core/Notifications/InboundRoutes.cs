using System.Text.Json;
using System.Text.Json.Serialization;
using System.Text.RegularExpressions;
using GnazzosWorld.Core.Networking;
using Microsoft.Data.Sqlite;

namespace GnazzosWorld.Core.Notifications;

public enum LinkKind { Catalog, Request }
public sealed record LinkTarget(LinkKind Kind, string Id)
{
    public string Canonical => $"gnazzosworld://open/{(Kind == LinkKind.Catalog ? "catalog" : "request")}/{Id}";
    public static LinkTarget? Parse(string? raw)
    {
        if (raw is null || raw.Length > 200) return null;
        var match = Regex.Match(raw, @"\Agnazzosworld://open/(catalog|request)/([A-Za-z0-9_-]{1,64})\z", RegexOptions.CultureInvariant);
        return match.Success ? new(match.Groups[1].Value == "catalog" ? LinkKind.Catalog : LinkKind.Request, match.Groups[2].Value) : null;
    }
    public static string PublicShare(string id) => RequestIdPolicy.IsValid(id)
        ? new LinkTarget(LinkKind.Catalog, id).Canonical : throw new ArgumentException("Use a bounded public catalog ID.");
}
public sealed record NotificationEnvelope(int Schema, string MessageId, string Link, long ExpiresUnix)
{
    private static readonly JsonSerializerOptions Options = new()
    { PropertyNamingPolicy = JsonNamingPolicy.CamelCase, UnmappedMemberHandling = JsonUnmappedMemberHandling.Disallow };
    public string Encode() => JsonSerializer.Serialize(this, Options);
    public bool IsValid(DateTimeOffset now) => Schema == 1 && Guid.TryParseExact(MessageId, "N", out _) &&
        LinkTarget.Parse(Link) is not null && ExpiresUnix > now.ToUnixTimeSeconds() && ExpiresUnix <= now.AddMinutes(10).ToUnixTimeSeconds();
    public static NotificationEnvelope? Decode(string? text, DateTimeOffset now)
    {
        if (text is null || text.Length > 1024) return null;
        try
        {
            using var document = JsonDocument.Parse(text, new JsonDocumentOptions { MaxDepth = 4 });
            if (document.RootElement.ValueKind != JsonValueKind.Object) return null;
            var names = document.RootElement.EnumerateObject().Select(x => x.Name).ToArray();
            if (names.Distinct(StringComparer.Ordinal).Count() != names.Length) return null;
            var item = JsonSerializer.Deserialize<NotificationEnvelope>(text, Options); return item?.IsValid(now) == true ? item : null;
        }
        catch (JsonException) { return null; }
    }
    public static NotificationEnvelope Local(LinkTarget target, DateTimeOffset now) => new(1, Guid.NewGuid().ToString("N"), target.Canonical, now.AddMinutes(10).ToUnixTimeSeconds());
}
public sealed record PendingRoute(string MessageId, LinkTarget Target);

/// <summary>Bounded untrusted routing hints, not private content or authorization. One local inbox.</summary>
public sealed class RouteInbox : IDisposable
{
    private readonly SqliteConnection db;
    private readonly object sync = new();
    public RouteInbox(string path)
    {
        db = new(new SqliteConnectionStringBuilder { DataSource = path, Pooling = false }.ToString()); db.Open();
        using var command = db.CreateCommand(); command.CommandText = """
            CREATE TABLE IF NOT EXISTS inbound_routes (
              ordinal INTEGER PRIMARY KEY AUTOINCREMENT, id TEXT NOT NULL UNIQUE,
              link TEXT NOT NULL, expires INTEGER NOT NULL, done INTEGER NOT NULL DEFAULT 0);
            """; command.ExecuteNonQuery();
    }
    public bool Accept(NotificationEnvelope item, DateTimeOffset now)
    {
        if (!item.IsValid(now)) return false;
        lock (sync)
        {
            Prune(now);
            using var tx = db.BeginTransaction();
            using var check = db.CreateCommand(); check.Transaction = tx;
            check.CommandText = "SELECT COUNT(*), COALESCE(SUM(CASE WHEN done=0 THEN 1 ELSE 0 END),0) FROM inbound_routes";
            using (var reader = check.ExecuteReader())
            { reader.Read(); if (reader.GetInt64(0) >= 128 || reader.GetInt64(1) >= 8) return false; }
            using var insert = db.CreateCommand(); insert.Transaction = tx;
            insert.CommandText = """
                INSERT OR IGNORE INTO inbound_routes(id,link,expires)
                SELECT $id,$link,$expires WHERE NOT EXISTS
                  (SELECT 1 FROM inbound_routes WHERE done=0 AND link=$link);
                """;
            insert.Parameters.AddWithValue("$id", item.MessageId); insert.Parameters.AddWithValue("$link", item.Link);
            insert.Parameters.AddWithValue("$expires", item.ExpiresUnix);
            var accepted = insert.ExecuteNonQuery() == 1; tx.Commit(); return accepted;
        }
    }
    public PendingRoute? Peek(DateTimeOffset now)
    {
        lock (sync)
        {
            Prune(now); using var command = db.CreateCommand();
            command.CommandText = "SELECT id,link FROM inbound_routes WHERE done=0 ORDER BY ordinal LIMIT 1";
            using var reader = command.ExecuteReader(); if (!reader.Read()) return null;
            var target = LinkTarget.Parse(reader.GetString(1)) ?? throw new InvalidDataException("Stored route is invalid.");
            return new(reader.GetString(0), target);
        }
    }
    public void Complete(string id)
    {
        lock (sync) { using var command = db.CreateCommand(); command.CommandText = "UPDATE inbound_routes SET done=1 WHERE id=$id";
            command.Parameters.AddWithValue("$id", id); command.ExecuteNonQuery(); }
    }
    public void Clear()
    { lock (sync) { using var c = db.CreateCommand(); c.CommandText = "DELETE FROM inbound_routes"; c.ExecuteNonQuery(); } }
    private void Prune(DateTimeOffset now)
    { using var c = db.CreateCommand(); c.CommandText = "DELETE FROM inbound_routes WHERE expires<=$now";
        c.Parameters.AddWithValue("$now", now.ToUnixTimeSeconds()); c.ExecuteNonQuery(); }
    public void Dispose() => db.Dispose();
}

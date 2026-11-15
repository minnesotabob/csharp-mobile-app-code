using Microsoft.Data.Sqlite;

namespace GnazzosWorld.Core.Media;

/// <summary>Small anonymous-device lab store; not encrypted, account-scoped or synchronized.</summary>
public sealed class AudioProgressDatabase : IDisposable
{
    private readonly SqliteConnection connection;
    private readonly object gate = new();
    public AudioProgressDatabase(string path)
    {
        connection = new(new SqliteConnectionStringBuilder { DataSource = path, Pooling = false }.ToString());
        connection.Open();
        using var command = connection.CreateCommand();
        command.CommandText = """
            CREATE TABLE IF NOT EXISTS audio_progress(
              item_id TEXT NOT NULL, media_revision TEXT NOT NULL,
              position_ticks INTEGER NOT NULL, duration_ticks INTEGER NOT NULL,
              completed INTEGER NOT NULL, PRIMARY KEY(item_id,media_revision));
            """;
        command.ExecuteNonQuery();
    }
    public AudioCheckpoint? Read(AudioItem item)
    {
        lock (gate)
        {
            using var command = ForItem(item, """
                SELECT position_ticks,duration_ticks,completed FROM audio_progress
                WHERE item_id=$id AND media_revision=$revision
                """);
            using var reader = command.ExecuteReader(); if (!reader.Read()) return null;
            var checkpoint = new AudioCheckpoint(item.Id, item.Revision, reader.GetInt64(0), reader.GetInt64(1), reader.GetBoolean(2));
            PlaybackSession.ValidateCheckpoint(checkpoint); return checkpoint;
        }
    }
    public void Save(AudioCheckpoint checkpoint)
    {
        PlaybackSession.ValidateCheckpoint(checkpoint);
        lock (gate)
        {
            using var command = connection.CreateCommand();
            command.CommandText = """
                INSERT INTO audio_progress VALUES($id,$revision,$position,$duration,$completed)
                ON CONFLICT(item_id,media_revision) DO UPDATE SET position_ticks=$position,
                    duration_ticks=$duration,completed=$completed
                """;
            command.Parameters.AddWithValue("$id", checkpoint.ItemId);
            command.Parameters.AddWithValue("$revision", checkpoint.Revision);
            command.Parameters.AddWithValue("$position", checkpoint.PositionTicks);
            command.Parameters.AddWithValue("$duration", checkpoint.DurationTicks);
            command.Parameters.AddWithValue("$completed", checkpoint.Completed); command.ExecuteNonQuery();
        }
    }
    public void Remove(AudioItem item)
    {
        lock (gate)
        {
            using var command = ForItem(item, "DELETE FROM audio_progress WHERE item_id=$id AND media_revision=$revision");
            command.ExecuteNonQuery();
        }
    }
    private SqliteCommand ForItem(AudioItem item, string sql)
    {
        var command = connection.CreateCommand(); command.CommandText = sql;
        command.Parameters.AddWithValue("$id", item.Id); command.Parameters.AddWithValue("$revision", item.Revision); return command;
    }
    public void Dispose() { lock (gate) connection.Dispose(); }
}

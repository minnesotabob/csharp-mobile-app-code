using GnazzosWorld.Core.Sync;
using Microsoft.Data.Sqlite;

// Everything here is a fictitious local fixture. No login, network, or customer data.
var directory = Path.Combine(Path.GetTempPath(), "gnazzo-sync-lab-" + Guid.NewGuid().ToString("N"));
try
{
    var server = new SimulatedSyncServer();
    var transport = server.ForSimulatedUser("demo-reader");
    var phone = new UserSyncReplica(Path.Combine(directory, "phone.sqlite3"), "demo-reader");
    var tablet = new UserSyncReplica(Path.Combine(directory, "tablet.sqlite3"), "demo-reader");
    var draft = new UserDataKey(UserDataKind.Draft, "anniversary-demo");
    phone.Queue(draft, "Phone verse"); tablet.Queue(draft, "Tablet chorus");
    await phone.SyncAsync(transport);
    await tablet.SyncAsync(transport);
    var conflict = tablet.View(draft);
    Console.WriteLine($"Conflict: {conflict.Conflict}; local={conflict.Value}; remote={conflict.ServerValue}");
    await tablet.ResolveAsync(draft, keepLocal: true);
    await tablet.SyncAsync(transport); await phone.SyncAsync(transport);
    Console.WriteLine($"Resolved draft on phone: {phone.View(draft).Value}");

    var song = new UserDataKey(UserDataKind.Favorite, "song-northern-lights");
    phone.Queue(song, "saved"); await phone.SyncAsync(transport); await tablet.SyncAsync(transport);
    tablet.Queue(song, "saved"); // A stale intention, created before the deletion is observed.
    phone.Queue(song, null); await phone.SyncAsync(transport); await tablet.SyncAsync(transport);
    Console.WriteLine($"Stale favorite conflicts with deletion: {tablet.View(song).Conflict}");
    await tablet.ResolveAsync(song, keepLocal: false);
    Console.WriteLine($"Favorite remains deleted: {tablet.View(song).Value is null}");
    Console.WriteLine("Local protocol fixture only; native and authenticated synchronization remain pending.");
}
finally
{
    SqliteConnection.ClearAllPools();
    if (Directory.Exists(directory)) Directory.Delete(directory, true);
}

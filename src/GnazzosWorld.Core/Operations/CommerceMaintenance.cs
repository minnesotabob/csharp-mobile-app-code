using System.Security.Cryptography;
using System.Text.Json;
using System.Text.Json.Serialization;
using GnazzosWorld.Core.Commerce;
using Microsoft.Data.Sqlite;

namespace GnazzosWorld.Core.Operations;

public sealed record BackupManifest(int Schema,int DatabaseVersion,long Length,string Sha256,DateTimeOffset CreatedAt);
public sealed record MaintenanceState(string Generation,bool RestorePending);
/// <summary>Explicit local synthetic database operations. No live-file replacement, secret export or remote admin route.</summary>
public static class CommerceMaintenance
{
    public const int Version=1;public const long MaxDatabaseBytes=64*1024*1024;
    private static readonly string[] Baseline=["commerce_quotes","commerce_orders","commerce_history","commerce_payments","commerce_payment_events","commerce_revisions","commerce_deliveries","commerce_notices","commerce_delivery_grants"];
    private static readonly string[] Added=["commerce_schema_migrations","commerce_maintenance"];
    private static readonly JsonSerializerOptions Json=new(JsonSerializerDefaults.Web){UnmappedMemberHandling=JsonUnmappedMemberHandling.Disallow};
    public static void CheckVersion(SqliteConnection db)
    {using var c=db.CreateCommand();c.CommandText="PRAGMA user_version";if((long)c.ExecuteScalar()! is <0 or >Version)throw new InvalidDataException("Unsupported commerce database version.");}
    private static SqliteConnection Open(string path,bool readOnly)
    {CheckFile(path);var db=new SqliteConnection(new SqliteConnectionStringBuilder{DataSource=Path.GetFullPath(path),Mode=readOnly?SqliteOpenMode.ReadOnly:SqliteOpenMode.ReadWrite,Pooling=false,DefaultTimeout=2}.ToString());db.Open();return db;}
    private static void CheckFile(string path)
    {var f=new FileInfo(path);if(!f.Exists||f.Length is <=0 or >MaxDatabaseBytes||(f.Attributes&FileAttributes.ReparsePoint)!=0)throw new InvalidDataException("Expected one bounded local database file.");}
    private static long Scalar(SqliteConnection db,string sql){using var c=db.CreateCommand();c.CommandText=sql;return (long)c.ExecuteScalar()!;}
    private static void Tables(SqliteConnection db,bool migrated)
    {
        using var c=db.CreateCommand();c.CommandText="SELECT name FROM sqlite_master WHERE type='table' AND name NOT LIKE 'sqlite_%'";using var r=c.ExecuteReader();var names=new HashSet<string>(StringComparer.Ordinal);while(r.Read())names.Add(r.GetString(0));
        if(!names.SetEquals(migrated?Baseline.Concat(Added):Baseline))throw new InvalidDataException("Unexpected commerce schema.");
    }
    public static void Initialize(string path)
    {
        if(File.Exists(path))throw new IOException("Initialization never overwrites an existing database.");Directory.CreateDirectory(Path.GetDirectoryName(Path.GetFullPath(path))!);
        using(var reserved=new FileStream(path,FileMode.CreateNew,FileAccess.Write)){}
        using(var store=new SqliteOrderStore(path,TimeProvider.System)){}
        Upgrade(path);
    }
    public static void Upgrade(string path)
    {
        using var db=Open(path,false);CheckVersion(db);var version=Scalar(db,"PRAGMA user_version");Tables(db,version==Version);
        if(version==Version){_=State(db);return;}
        using var tx=db.BeginTransaction(deferred:false);using var c=db.CreateCommand();c.Transaction=tx;c.CommandText="""
            CREATE TABLE commerce_schema_migrations(version INTEGER PRIMARY KEY,at_ticks INTEGER NOT NULL);
            CREATE TABLE commerce_maintenance(id INTEGER PRIMARY KEY CHECK(id=1),generation TEXT NOT NULL,restore_pending INTEGER NOT NULL);
            INSERT INTO commerce_schema_migrations VALUES(1,$now);
            INSERT INTO commerce_maintenance VALUES(1,$generation,0);
            PRAGMA user_version=1;
            """;c.Parameters.AddWithValue("$now",DateTimeOffset.UtcNow.UtcTicks);c.Parameters.AddWithValue("$generation",Guid.NewGuid().ToString("N"));c.ExecuteNonQuery();tx.Commit();
    }
    private static MaintenanceState State(SqliteConnection db)
    {
        using var c=db.CreateCommand();c.CommandText="SELECT generation,restore_pending FROM commerce_maintenance WHERE id=1";using var r=c.ExecuteReader();
        if(!r.Read()||!OrderContract.Id(r.GetString(0))||r.GetInt64(1) is <0 or >1)throw new InvalidDataException("Invalid maintenance state.");return new(r.GetString(0),r.GetInt64(1)!=0);
    }
    public static MaintenanceState ReadState(string path)
    {using var db=Open(path,true);if(Scalar(db,"PRAGMA user_version")!=Version)throw new InvalidDataException("Explicit migration is required.");Tables(db,true);return State(db);}
    private static void Integrity(string path)
    {
        using var db=Open(path,true);if(Scalar(db,"PRAGMA user_version")!=Version)throw new InvalidDataException("Unsupported backup version.");Tables(db,true);_=State(db);
        using var c=db.CreateCommand();c.CommandText="PRAGMA quick_check";if(c.ExecuteScalar() is not string result||result!="ok")throw new InvalidDataException("Database integrity check failed.");
        c.CommandText="PRAGMA foreign_key_check";using var reader=c.ExecuteReader();if(reader.Read())throw new InvalidDataException("Database relationship check failed.");
    }
    public static BackupManifest Backup(string sourcePath,string newDirectory)
    {
        _=ReadState(sourcePath);var final=Path.GetFullPath(newDirectory);if(Directory.Exists(final)||File.Exists(final))throw new IOException("Backup destination must be new.");Directory.CreateDirectory(Path.GetDirectoryName(final)!);var staging=final+".partial-"+Guid.NewGuid().ToString("N");Directory.CreateDirectory(staging);
        try
        {
            var destination=Path.Combine(staging,"commerce.sqlite3");
            using(var source=Open(sourcePath,true))using(var target=new SqliteConnection(new SqliteConnectionStringBuilder{DataSource=destination,Pooling=false,DefaultTimeout=2}.ToString())){target.Open();source.BackupDatabase(target);}
            Integrity(destination);var bytes=File.ReadAllBytes(destination);var manifest=new BackupManifest(1,Version,bytes.LongLength,Convert.ToHexString(SHA256.HashData(bytes)).ToLowerInvariant(),DateTimeOffset.UtcNow);
            File.WriteAllText(Path.Combine(staging,"backup.json"),JsonSerializer.Serialize(manifest,Json));Directory.Move(staging,final);return manifest;
        }
        catch{if(Directory.Exists(staging))Directory.Delete(staging,true);throw;}
    }
    public static BackupManifest VerifyBackup(string directory)
    {
        var manifestPath=Path.Combine(directory,"backup.json");var info=new FileInfo(manifestPath);if(!info.Exists||info.Length>16384)throw new InvalidDataException("Missing bounded backup manifest.");
        var m=JsonSerializer.Deserialize<BackupManifest>(File.ReadAllText(manifestPath),Json)??throw new InvalidDataException("Invalid backup manifest.");
        var path=Path.Combine(directory,"commerce.sqlite3");CheckFile(path);if(m.Schema!=1||m.DatabaseVersion!=Version||m.Length!=new FileInfo(path).Length||m.Sha256 is null||m.Sha256.Length!=64||Convert.ToHexString(SHA256.HashData(File.ReadAllBytes(path))).ToLowerInvariant()!=m.Sha256)throw new InvalidDataException("Backup bytes do not match manifest.");Integrity(path);return m;
    }
    public static MaintenanceState Restore(string backupDirectory,string newDirectory)
    {
        var manifest=VerifyBackup(backupDirectory);var final=Path.GetFullPath(newDirectory);if(Directory.Exists(final)||File.Exists(final))throw new IOException("Restore destination must be new; never replace a running database.");Directory.CreateDirectory(Path.GetDirectoryName(final)!);var staging=final+".partial-"+Guid.NewGuid().ToString("N");Directory.CreateDirectory(staging);
        try
        {
            var path=Path.Combine(staging,"commerce.sqlite3");File.Copy(Path.Combine(backupDirectory,"commerce.sqlite3"),path,false);Integrity(path);
            // Recheck copied bytes: validation of a mutable source directory is not a lock.
            if(Convert.ToHexString(SHA256.HashData(File.ReadAllBytes(path))).ToLowerInvariant()!=manifest.Sha256)throw new InvalidDataException("Backup changed during restore.");
            var state=new MaintenanceState(Guid.NewGuid().ToString("N"),true);
            using(var db=Open(path,false)){using var tx=db.BeginTransaction(deferred:false);using var c=db.CreateCommand();c.Transaction=tx;c.CommandText="DELETE FROM commerce_delivery_grants; UPDATE commerce_maintenance SET generation=$generation,restore_pending=1 WHERE id=1";c.Parameters.AddWithValue("$generation",state.Generation);c.ExecuteNonQuery();tx.Commit();}
            Directory.Move(staging,final);return state;
        }
        catch{if(Directory.Exists(staging))Directory.Delete(staging,true);throw;}
    }
    public static void ReleaseRestoredForLab(string path,string expectedGeneration)
    {
        if(!OrderContract.Id(expectedGeneration))throw new InvalidDataException("Invalid restore generation.");Integrity(path);using var db=Open(path,false);using var tx=db.BeginTransaction(deferred:false);using var c=db.CreateCommand();c.Transaction=tx;c.CommandText="UPDATE commerce_maintenance SET restore_pending=0 WHERE id=1 AND generation=$generation AND restore_pending=1";c.Parameters.AddWithValue("$generation",expectedGeneration);if(c.ExecuteNonQuery()!=1)throw new InvalidOperationException("Restore state changed or was already released.");tx.Commit();
    }
}

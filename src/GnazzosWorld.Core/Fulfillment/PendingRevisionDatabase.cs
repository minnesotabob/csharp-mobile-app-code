using System.Text;
using System.Text.Json;
using GnazzosWorld.Core.Commerce;
using Microsoft.Data.Sqlite;

namespace GnazzosWorld.Core.Fulfillment;

public sealed record PendingRevision(RevisionCommand Command,RevisionReceipt? Confirmed=null,OrderProblem? Rejected=null)
{public override string ToString()=>"Saved revision [private note omitted]";}
/// <summary>One last revision per account/resource/order, at most 64 rows; plaintext synthetic notes, no tokens.</summary>
public sealed class PendingRevisionDatabase:IDisposable
{
    private readonly SqliteConnection db;private readonly object sync=new();
    public PendingRevisionDatabase(string path)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(Path.GetFullPath(path))!);db=new(new SqliteConnectionStringBuilder{DataSource=path,Pooling=false,DefaultTimeout=10}.ToString());db.Open();
        using var c=db.CreateCommand();c.CommandText="CREATE TABLE IF NOT EXISTS pending_revisions(issuer TEXT,subject TEXT,origin TEXT,audience TEXT,order_id TEXT,payload TEXT NOT NULL,PRIMARY KEY(issuer,subject,origin,audience,order_id))";c.ExecuteNonQuery();
    }
    public static bool Definitive(OrderProblem? p)=>p is OrderProblem.VersionConflict or OrderProblem.InvalidTransition or OrderProblem.Unavailable;
    private SqliteCommand Command(CommerceAccount a,string id,string sql,SqliteTransaction? tx=null)
    {a.Validate();if(!OrderContract.Id(id))throw new InvalidDataException("Invalid revision order.");var c=db.CreateCommand();c.Transaction=tx;c.CommandText=sql;c.Parameters.AddWithValue("$i",a.Owner.Issuer);c.Parameters.AddWithValue("$s",a.Owner.Subject);c.Parameters.AddWithValue("$o",a.ApiOrigin);c.Parameters.AddWithValue("$a",a.Audience);c.Parameters.AddWithValue("$id",id);return c;}
    private const string Where="issuer=$i AND subject=$s AND origin=$o AND audience=$a AND order_id=$id";
    private PendingRevision? ReadInside(CommerceAccount a,string id,SqliteTransaction? tx=null)
    {
        using var c=Command(a,id,"SELECT payload FROM pending_revisions WHERE "+Where,tx);if(c.ExecuteScalar() is not string text)return null;
        if(Encoding.UTF8.GetByteCount(text)>16384)throw new InvalidDataException("Saved revision exceeds policy.");
        var p=JsonSerializer.Deserialize<PendingRevision>(text,OrderContract.Json)??throw new InvalidDataException("Invalid saved revision.");
        if(p.Command is null||p.Command.ExpectedVersion<=0||!OrderContract.Id(p.Command.OperationKey)||p.Command.Note!=TrackingContract.Note(p.Command.Note)||(p.Confirmed is not null&&p.Rejected is not null)||(p.Rejected is not null&&!Definitive(p.Rejected)))throw new InvalidDataException("Invalid saved revision state.");
        if(p.Confirmed is not null)TrackingContract.Revision(p.Confirmed,id,p.Command);return p;
    }
    public PendingRevision? Read(CommerceAccount a,string id){lock(sync)return ReadInside(a,id);}
    private void Save(CommerceAccount a,string id,PendingRevision p,SqliteTransaction tx)
    {var text=JsonSerializer.Serialize(p,OrderContract.Json);if(Encoding.UTF8.GetByteCount(text)>16384)throw new InvalidDataException("Revision exceeds local policy.");using var c=Command(a,id,"INSERT INTO pending_revisions VALUES($i,$s,$o,$a,$id,$p) ON CONFLICT(issuer,subject,origin,audience,order_id) DO UPDATE SET payload=excluded.payload",tx);c.Parameters.AddWithValue("$p",text);c.ExecuteNonQuery();}
    public PendingRevision Begin(CommerceAccount a,string id,long version,string note)
    {
        note=TrackingContract.Note(note);if(version<=0)throw new InvalidDataException("Invalid expected version.");
        lock(sync){using var tx=db.BeginTransaction(deferred:false);var old=ReadInside(a,id,tx);
            if(old is not null&&old.Command.ExpectedVersion==version&&old.Command.Note==note){tx.Commit();return old;}
            if(old is {Confirmed:null,Rejected:null})throw new InvalidOperationException("Reconcile this order's saved revision first.");
            if(old is null){using var count=db.CreateCommand();count.Transaction=tx;count.CommandText="SELECT COUNT(*) FROM pending_revisions";if((long)count.ExecuteScalar()!>=64)throw new IOException("Revision journal is full.");}
            var p=new PendingRevision(new(version,Guid.NewGuid().ToString("N"),note));Save(a,id,p,tx);tx.Commit();return p;}
    }
    public void Finish(CommerceAccount a,string id,string key,RevisionReceipt? receipt,OrderProblem? rejected=null)
    {
        if((receipt is null)==(rejected is null)||(rejected is not null&&!Definitive(rejected)))throw new InvalidDataException("Revision outcome is unresolved.");
        lock(sync){using var tx=db.BeginTransaction(deferred:false);var p=ReadInside(a,id,tx)??throw new InvalidDataException("No saved revision.");if(p.Command.OperationKey!=key)throw new InvalidOperationException("Saved revision changed.");
            if(p.Confirmed is not null&&(receipt is null||p.Confirmed!=receipt))throw new InvalidDataException("Confirmed revision changed.");
            if(p.Rejected is not null&&receipt is not null)throw new InvalidDataException("Rejected revision changed.");
            if(receipt is not null)TrackingContract.Revision(receipt,id,p.Command);Save(a,id,p with{Confirmed=receipt,Rejected=rejected},tx);tx.Commit();}
    }
    public void Dispose()=>db.Dispose();
}

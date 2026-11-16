using System.Text;
using System.Text.Json;
using Microsoft.Data.Sqlite;

namespace GnazzosWorld.Core.Commerce;

public sealed record CommerceAccount(OrderOwner Owner,string ApiOrigin,string Audience)
{
    public void Validate()
    {
        Owner.Validate();
        if(!Uri.TryCreate(ApiOrigin,UriKind.Absolute,out var uri)||uri.Scheme!="https"||uri.UserInfo.Length!=0||uri.AbsolutePath!="/"||uri.Query.Length!=0||uri.Fragment.Length!=0||
            !Uri.TryCreate(Audience,UriKind.Absolute,out var audience)||audience.Scheme!="https"||Audience.Length>256)throw new InvalidDataException("Invalid account/resource partition.");
    }
    public override string ToString()=>"Commerce partition [identity omitted]";
}
public sealed record PendingOrderOperation(int Schema,OrderQuote Quote,string OperationKey,OrderSnapshot? Confirmed=null,OrderProblem? Rejected=null)
{ public override string ToString()=>"Order operation [private data omitted]"; }
/// <summary>One last acceptance/receipt per issuer+subject+API origin+audience. Synthetic plaintext lab data; never tokens.</summary>
public sealed class PendingOrderDatabase:IDisposable
{
    private readonly SqliteConnection db;private readonly object sync=new();
    public PendingOrderDatabase(string path)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(Path.GetFullPath(path))!);
        db=new(new SqliteConnectionStringBuilder{DataSource=path,Pooling=false,DefaultTimeout=10}.ToString());db.Open();
        using var c=db.CreateCommand();c.CommandText="CREATE TABLE IF NOT EXISTS pending_order_operations(issuer TEXT,subject TEXT,origin TEXT,audience TEXT,payload TEXT NOT NULL,PRIMARY KEY(issuer,subject,origin,audience))";c.ExecuteNonQuery();
    }
    public static bool Definitive(OrderProblem? p)=>p is OrderProblem.ExpiredQuote or OrderProblem.ChangedRequest or OrderProblem.Unavailable;
    private SqliteCommand Command(CommerceAccount a,string sql,SqliteTransaction? tx=null)
    { var c=db.CreateCommand();c.CommandText=sql;c.Transaction=tx;c.Parameters.AddWithValue("$i",a.Owner.Issuer);c.Parameters.AddWithValue("$s",a.Owner.Subject);c.Parameters.AddWithValue("$o",a.ApiOrigin);c.Parameters.AddWithValue("$a",a.Audience);return c; }
    private PendingOrderOperation? ReadInside(CommerceAccount a,SqliteTransaction? tx=null)
    {
        using var c=Command(a,"SELECT payload FROM pending_order_operations WHERE issuer=$i AND subject=$s AND origin=$o AND audience=$a",tx);
        if(c.ExecuteScalar() is not string text)return null;
        if(Encoding.UTF8.GetByteCount(text)>65536)throw new InvalidDataException("Stored order operation exceeds policy.");
        var p=JsonSerializer.Deserialize<PendingOrderOperation>(text,OrderContract.Json)??throw new InvalidDataException("Invalid pending operation.");
        OrderApiClient.Validate(p.Quote);
        if(p.Schema!=1||!OrderContract.Id(p.OperationKey)||(p.Confirmed is not null&&p.Rejected is not null)||(p.Rejected is not null&&!Definitive(p.Rejected)))throw new InvalidDataException("Invalid pending operation state.");
        if(p.Confirmed is not null)OrderApiClient.Validate(p.Confirmed,p.Quote,p.OperationKey);return p;
    }
    public PendingOrderOperation? Read(CommerceAccount account)
    { account.Validate();lock(sync)return ReadInside(account); }
    private void SaveInside(CommerceAccount a,PendingOrderOperation p,SqliteTransaction tx)
    {
        var text=JsonSerializer.Serialize(p,OrderContract.Json);if(Encoding.UTF8.GetByteCount(text)>65536)throw new InvalidDataException("Pending operation exceeds policy.");
        using var c=Command(a,"INSERT INTO pending_order_operations VALUES($i,$s,$o,$a,$p) ON CONFLICT(issuer,subject,origin,audience) DO UPDATE SET payload=excluded.payload",tx);c.Parameters.AddWithValue("$p",text);c.ExecuteNonQuery();
    }
    public PendingOrderOperation Begin(CommerceAccount account,OrderQuote quote)
    {
        account.Validate();OrderApiClient.Validate(quote);
        lock(sync)
        {
            using var tx=db.BeginTransaction(deferred:false);var old=ReadInside(account,tx);
            if(old is not null && old.Quote.Id==quote.Id)
            { if(old.Quote!=quote)throw new InvalidDataException("Quote identity changed.");tx.Commit();return old; }
            if(old is {Confirmed:null,Rejected:null})throw new InvalidOperationException("Reconcile the saved acceptance before another request.");
            if(old is null){using var count=db.CreateCommand();count.Transaction=tx;count.CommandText="SELECT COUNT(*) FROM pending_order_operations";if((long)count.ExecuteScalar()!>=16)throw new IOException("Pending account/resource partition limit reached.");}
            var p=new PendingOrderOperation(1,quote,Guid.NewGuid().ToString("N"));SaveInside(account,p,tx);tx.Commit();return p;
        }
    }
    public void Confirm(CommerceAccount account,string key,OrderSnapshot order)=>Finish(account,key,order,null);
    public void Reject(CommerceAccount account,string key,OrderProblem problem)
    { if(!Definitive(problem))throw new InvalidOperationException("This outcome still needs reconciliation.");Finish(account,key,null,problem); }
    private void Finish(CommerceAccount a,string key,OrderSnapshot? order,OrderProblem? rejected)
    {
        a.Validate();lock(sync)
        {
            using var tx=db.BeginTransaction(deferred:false);var p=ReadInside(a,tx);
            if(p is null||p.OperationKey!=key)throw new InvalidOperationException("Acceptance operation changed.");
            if(order is not null)
            {
                OrderApiClient.Validate(order,p.Quote,key);
                if(p.Confirmed is { } prior && (prior.Id!=order.Id || order.Version<prior.Version ||
                    (order.Version==prior.Version && prior!=order))) throw new InvalidDataException("Receipt identity/version regressed.");
            }
            else if(p.Confirmed is not null)throw new InvalidOperationException("A confirmed acceptance cannot become a rejection.");
            SaveInside(a,p with{Confirmed=order,Rejected=rejected},tx);tx.Commit();
        }
    }
    public void Dispose()=>db.Dispose();
}

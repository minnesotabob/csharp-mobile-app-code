using System.Text.Json;
using Microsoft.Data.Sqlite;
using GnazzosWorld.Core.Orders;

namespace GnazzosWorld.Core.Commerce;

/// <summary>Single-file teaching store. SQLite transactions/constraints, not an in-process lock, serialize independent writers.</summary>
public sealed partial class SqliteOrderStore : IDisposable
{
    private readonly SqliteConnection db;
    private readonly TimeProvider clock;
    private readonly IOrderPricing pricing;
    private readonly object sync = new();
    public SqliteOrderStore(string path, TimeProvider clock, IOrderPricing? pricing = null)
    {
        this.clock = clock; this.pricing = pricing ?? new WebsiteSnapshotOrderPricing();
        Directory.CreateDirectory(Path.GetDirectoryName(Path.GetFullPath(path))!);
        db = new(new SqliteConnectionStringBuilder { DataSource = path, Pooling = false, DefaultTimeout = 10 }.ToString()); db.Open();
        try{GnazzosWorld.Core.Operations.CommerceMaintenance.CheckVersion(db);}catch{db.Dispose();throw;}
        using var command = db.CreateCommand(); command.CommandText = """
            PRAGMA foreign_keys=ON;
            CREATE TABLE IF NOT EXISTS commerce_quotes (
              id TEXT PRIMARY KEY, issuer TEXT NOT NULL, subject TEXT NOT NULL,
              request_json TEXT NOT NULL, request_hash TEXT NOT NULL, pricing TEXT NOT NULL,
              currency TEXT NOT NULL, total_minor INTEGER NOT NULL CHECK(total_minor>0), expires_ticks INTEGER NOT NULL);
            CREATE TABLE IF NOT EXISTS commerce_orders (
              id TEXT PRIMARY KEY, issuer TEXT NOT NULL, subject TEXT NOT NULL,
              quote_id TEXT NOT NULL UNIQUE REFERENCES commerce_quotes(id), operation_key TEXT NOT NULL,
              request_json TEXT NOT NULL, request_hash TEXT NOT NULL, pricing TEXT NOT NULL,
              currency TEXT NOT NULL, total_minor INTEGER NOT NULL CHECK(total_minor>0),
              state TEXT NOT NULL, version INTEGER NOT NULL CHECK(version>0), created_ticks INTEGER NOT NULL,
              UNIQUE(issuer,subject,operation_key));
            CREATE TABLE IF NOT EXISTS commerce_history (
              order_id TEXT NOT NULL REFERENCES commerce_orders(id), version INTEGER NOT NULL,
              state TEXT NOT NULL, at_ticks INTEGER NOT NULL, PRIMARY KEY(order_id,version));
            """; command.ExecuteNonQuery(); InitializePayments(); InitializeFulfillment();
    }
    private SqliteCommand Command(string sql, SqliteTransaction? tx, params (string Key, object Value)[] values)
    { var c = db.CreateCommand(); c.CommandText = sql; c.Transaction = tx; foreach (var (key,value) in values) c.Parameters.AddWithValue(key,value); return c; }
    public OrderQuote CreateQuote(OrderOwner owner, SongRequest request)
    {
        owner.Validate(); var normalized = OrderContract.Normalize(request); var hash = OrderContract.Fingerprint(normalized);
        lock (sync)
        {
            using var tx = db.BeginTransaction(deferred: false);
            using var count = Command("SELECT COUNT(*),COALESCE(SUM(CASE WHEN issuer=$i AND subject=$s THEN 1 ELSE 0 END),0) FROM commerce_quotes", tx, ("$i", owner.Issuer), ("$s", owner.Subject));
            using (var r = count.ExecuteReader()) { r.Read(); if (r.GetInt64(0) >= 1000 || r.GetInt64(1) >= 100) throw new OrderRejectedException(OrderProblem.Capacity); }
            var total = pricing.TotalMinor(normalized); var version = pricing.Version;
            if (total is <= 0 or > 1000000 || string.IsNullOrWhiteSpace(version) || version.Length > 64 || version.Any(char.IsControl)) throw new InvalidDataException("Invalid server pricing policy.");
            var quote = new OrderQuote(Guid.NewGuid().ToString("N"), normalized, hash, version, "USD", total, clock.GetUtcNow().AddMinutes(15));
            using var insert = Command("INSERT INTO commerce_quotes VALUES($id,$i,$s,$request,$hash,$pricing,$currency,$total,$expires)", tx,
                ("$id",quote.Id),("$i",owner.Issuer),("$s",owner.Subject),("$request",JsonSerializer.Serialize(normalized,OrderContract.Json)),
                ("$hash",hash),("$pricing",version),("$currency",quote.Currency),("$total",total),("$expires",quote.ExpiresAt.UtcTicks));
            insert.ExecuteNonQuery(); tx.Commit(); return quote;
        }
    }
    public OrderSnapshot Accept(OrderOwner owner, AcceptOrderCommand attempt)
    {
        owner.Validate();
        if (!OrderContract.Id(attempt.QuoteId) || !OrderContract.Id(attempt.OperationKey)) throw new OrderRejectedException(OrderProblem.InvalidRequest);
        var normalized = OrderContract.Normalize(attempt.Request); var hash = OrderContract.Fingerprint(normalized);
        lock (sync)
        {
            using var tx = db.BeginTransaction(deferred: false);
            var replay = FindOrder("issuer=$i AND subject=$s AND operation_key=$key", tx, ("$i",owner.Issuer),("$s",owner.Subject),("$key",attempt.OperationKey));
            if (replay is not null)
            {
                if (replay.QuoteId != attempt.QuoteId || replay.RequestHash != hash) throw new OrderRejectedException(OrderProblem.KeyConflict);
                tx.Commit(); return replay; // Replay before quote expiry; an accepted request remains accepted.
            }
            OrderQuote quote;
            using (var read = Command("SELECT id,request_json,request_hash,pricing,currency,total_minor,expires_ticks FROM commerce_quotes WHERE id=$id AND issuer=$i AND subject=$s",tx,
                ("$id",attempt.QuoteId),("$i",owner.Issuer),("$s",owner.Subject)))
            using (var r = read.ExecuteReader())
            {
                if (!r.Read()) throw new OrderRejectedException(OrderProblem.Unavailable);
                quote = new(r.GetString(0),JsonSerializer.Deserialize<SongRequest>(r.GetString(1),OrderContract.Json)!,r.GetString(2),r.GetString(3),r.GetString(4),r.GetInt64(5),new(r.GetInt64(6),TimeSpan.Zero));
            }
            if (quote.RequestHash != hash) throw new OrderRejectedException(OrderProblem.ChangedRequest);
            if (clock.GetUtcNow() >= quote.ExpiresAt) throw new OrderRejectedException(OrderProblem.ExpiredQuote);
            using (var used = Command("SELECT COUNT(*) FROM commerce_orders WHERE quote_id=$id",tx,("$id",quote.Id)))
                if ((long)used.ExecuteScalar()! != 0) throw new OrderRejectedException(OrderProblem.QuoteUsed);
            var order = new OrderSnapshot(Guid.NewGuid().ToString("N"),quote.Id,attempt.OperationKey,quote.Request,quote.RequestHash,quote.PricingVersion,quote.Currency,quote.TotalMinor,OrderState.Accepted,1,clock.GetUtcNow());
            using var insert = Command("INSERT INTO commerce_orders VALUES($id,$i,$s,$quote,$key,$request,$hash,$pricing,$currency,$total,$state,1,$created)",tx,
                ("$id",order.Id),("$i",owner.Issuer),("$s",owner.Subject),("$quote",order.QuoteId),("$key",order.OperationKey),
                ("$request",JsonSerializer.Serialize(order.Request,OrderContract.Json)),("$hash",order.RequestHash),("$pricing",order.PricingVersion),("$currency",order.Currency),
                ("$total",order.TotalMinor),("$state",order.State.ToString()),("$created",order.CreatedAt.UtcTicks));
            insert.ExecuteNonQuery(); AddHistory(order.Id,1,order.State,tx); tx.Commit(); return order;
        }
    }
    private OrderSnapshot? FindOrder(string where, SqliteTransaction? tx, params (string Key, object Value)[] values)
    {
        // where is selected only by this class; no caller-supplied SQL expression.
        using var command = Command("SELECT id,quote_id,operation_key,request_json,request_hash,pricing,currency,total_minor,state,version,created_ticks FROM commerce_orders WHERE " + where,tx,values);
        using var r = command.ExecuteReader();
        return !r.Read() ? null : new(r.GetString(0),r.GetString(1),r.GetString(2),JsonSerializer.Deserialize<SongRequest>(r.GetString(3),OrderContract.Json)!,
            r.GetString(4),r.GetString(5),r.GetString(6),r.GetInt64(7),Enum.Parse<OrderState>(r.GetString(8)),r.GetInt64(9),new(r.GetInt64(10),TimeSpan.Zero));
    }
    public OrderSnapshot? Get(OrderOwner owner, string id)
    {
        owner.Validate(); if (!OrderContract.Id(id)) throw new OrderRejectedException(OrderProblem.InvalidRequest);
        lock (sync) return FindOrder("id=$id AND issuer=$i AND subject=$s",null,("$id",id),("$i",owner.Issuer),("$s",owner.Subject));
    }
    /// <summary>Trusted controlled fixture ONLY. No customer HTTP transition route or real payment evidence is supplied.</summary>
    public OrderSnapshot AdvanceForLab(string id, long expectedVersion, OrderState target)
    {
        lock (sync)
        {
            using var tx = db.BeginTransaction(deferred:false);
            var current = FindOrder("id=$id",tx,("$id",id)) ?? throw new OrderRejectedException(OrderProblem.Unavailable);
            if (current.Version != expectedVersion) throw new OrderRejectedException(OrderProblem.VersionConflict);
            if (!OrderContract.CanTransition(current.State,target)) throw new OrderRejectedException(OrderProblem.InvalidTransition);
            var next = checked(current.Version+1);
            using var update = Command("UPDATE commerce_orders SET state=$state,version=$next WHERE id=$id AND version=$expected",tx,
                ("$state",target.ToString()),("$next",next),("$id",id),("$expected",expectedVersion));
            if (update.ExecuteNonQuery()!=1) throw new OrderRejectedException(OrderProblem.VersionConflict);
            AddHistory(id,next,target,tx); tx.Commit(); return current with {State=target,Version=next};
        }
    }
    private void AddHistory(string id,long version,OrderState state,SqliteTransaction tx)
    { using var c=Command("INSERT INTO commerce_history VALUES($id,$version,$state,$at)",tx,("$id",id),("$version",version),("$state",state.ToString()),("$at",clock.GetUtcNow().UtcTicks)); c.ExecuteNonQuery(); }
    public IReadOnlyList<OrderHistory> HistoryForLab(string id)
    {
        lock(sync) { using var c=Command("SELECT version,state,at_ticks FROM commerce_history WHERE order_id=$id ORDER BY version",null,("$id",id)); using var r=c.ExecuteReader(); var result=new List<OrderHistory>();
            while(r.Read()) result.Add(new(r.GetInt64(0),Enum.Parse<OrderState>(r.GetString(1)),new(r.GetInt64(2),TimeSpan.Zero))); return result; }
    }
    public void Dispose()=>db.Dispose();
}

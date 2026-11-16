using System.Security.Cryptography;
using System.Text;
using GnazzosWorld.Core.Fulfillment;
using GnazzosWorld.Core.Payments;
using Microsoft.Data.Sqlite;

namespace GnazzosWorld.Core.Commerce;

public sealed partial class SqliteOrderStore
{
    private void InitializeFulfillment()
    {
        using var c=db.CreateCommand();c.CommandText="""
            CREATE TABLE IF NOT EXISTS commerce_revisions (
              id TEXT PRIMARY KEY,order_id TEXT NOT NULL REFERENCES commerce_orders(id),operation_key TEXT NOT NULL,
              base_version INTEGER NOT NULL,applied_version INTEGER NOT NULL,note TEXT NOT NULL,at_ticks INTEGER NOT NULL,
              UNIQUE(order_id,operation_key));
            CREATE TABLE IF NOT EXISTS commerce_deliveries (
              id TEXT PRIMARY KEY,order_id TEXT NOT NULL UNIQUE REFERENCES commerce_orders(id),version INTEGER NOT NULL,
              revoked INTEGER NOT NULL DEFAULT 0);
            CREATE TABLE IF NOT EXISTS commerce_notices (
              id TEXT PRIMARY KEY,order_id TEXT NOT NULL REFERENCES commerce_orders(id),version INTEGER NOT NULL,
              kind TEXT NOT NULL,at_ticks INTEGER NOT NULL,UNIQUE(order_id,version,kind));
            CREATE TABLE IF NOT EXISTS commerce_delivery_grants (
              token_hash TEXT PRIMARY KEY,delivery_id TEXT NOT NULL REFERENCES commerce_deliveries(id),
              issuer TEXT NOT NULL,subject TEXT NOT NULL,order_version INTEGER NOT NULL,expires_ticks INTEGER NOT NULL);
            """;c.ExecuteNonQuery();
    }
    private OrderSnapshot Owned(OrderOwner owner,string id,SqliteTransaction? tx)
    {
        owner.Validate();if(!OrderContract.Id(id))throw new OrderRejectedException(OrderProblem.InvalidRequest);
        return FindOrder("id=$id AND issuer=$i AND subject=$s",tx,("$id",id),("$i",owner.Issuer),("$s",owner.Subject))??throw new OrderRejectedException(OrderProblem.Unavailable);
    }
    public OrderPage ListOrders(OrderOwner owner,string? after=null)
    {
        owner.Validate();if(after is not null&&!OrderContract.Id(after))throw new OrderRejectedException(OrderProblem.InvalidRequest);
        lock(sync)
        {
            using var c=Command("SELECT id,state,version,created_ticks FROM commerce_orders WHERE issuer=$i AND subject=$s AND id>$after ORDER BY id LIMIT 21",null,("$i",owner.Issuer),("$s",owner.Subject),("$after",after??""));using var r=c.ExecuteReader();var rows=new List<OrderSummary>();
            while(r.Read())rows.Add(new(r.GetString(0),Enum.Parse<OrderState>(r.GetString(1)),r.GetInt64(2),new(r.GetInt64(3),TimeSpan.Zero)));
            return new(rows.Take(20).ToArray(),rows.Count>20?rows[19].Id:null); // Live keyset pages, not a frozen multi-page snapshot.
        }
    }
    private RevisionReceipt? RevisionByKey(string orderId,string key,SqliteTransaction tx)
    {
        using var c=Command("SELECT id,operation_key,base_version,applied_version,note,at_ticks FROM commerce_revisions WHERE order_id=$id AND operation_key=$key",tx,("$id",orderId),("$key",key));using var r=c.ExecuteReader();return !r.Read()?null:ReadRevision(r,orderId);
    }
    private static RevisionReceipt ReadRevision(SqliteDataReader r,string orderId)=>new(r.GetString(0),orderId,r.GetString(1),r.GetInt64(2),r.GetInt64(3),r.GetString(4),new(r.GetInt64(5),TimeSpan.Zero));
    private void HistoryBudget(string orderId,SqliteTransaction tx)
    {using var c=Command("SELECT COUNT(*) FROM commerce_history WHERE order_id=$id",tx,("$id",orderId));if((long)c.ExecuteScalar()!>=100)throw new OrderRejectedException(OrderProblem.Capacity);}
    private void Notice(string orderId,long version,OrderNoticeKind kind,SqliteTransaction tx)
    {using var c=Command("INSERT INTO commerce_notices VALUES($id,$order,$version,$kind,$at)",tx,("$id",Guid.NewGuid().ToString("N")),("$order",orderId),("$version",version),("$kind",kind.ToString()),("$at",clock.GetUtcNow().UtcTicks));c.ExecuteNonQuery();}
    private void SetState(OrderSnapshot order,OrderState state,SqliteTransaction tx)
    {
        HistoryBudget(order.Id,tx);using var c=Command("UPDATE commerce_orders SET state=$state,version=version+1 WHERE id=$id AND version=$version",tx,("$state",state.ToString()),("$id",order.Id),("$version",order.Version));
        if(c.ExecuteNonQuery()!=1)throw new OrderRejectedException(OrderProblem.VersionConflict);AddHistory(order.Id,checked(order.Version+1),state,tx);
    }
    public RevisionReceipt RequestRevision(OrderOwner owner,string orderId,RevisionCommand command)
    {
        var note=TrackingContract.Note(command.Note);if(!OrderContract.Id(command.OperationKey)||command.ExpectedVersion<=0)throw new OrderRejectedException(OrderProblem.InvalidRequest);
        lock(sync)
        {
            using var tx=db.BeginTransaction(deferred:false);var order=Owned(owner,orderId,tx);
            var replay=RevisionByKey(orderId,command.OperationKey,tx);
            if(replay is not null){if(replay.BaseVersion!=command.ExpectedVersion||replay.Note!=note)throw new OrderRejectedException(OrderProblem.KeyConflict);tx.Commit();return replay;}
            if(order.Version!=command.ExpectedVersion)throw new OrderRejectedException(OrderProblem.VersionConflict);
            if(order.State!=OrderState.ReadyForReview||Payment(orderId,tx)?.State!=TestPaymentState.Paid)throw new OrderRejectedException(OrderProblem.InvalidTransition);
            using(var count=Command("SELECT COUNT(*) FROM commerce_revisions WHERE order_id=$id",tx,("$id",orderId)))if((long)count.ExecuteScalar()!>=10)throw new OrderRejectedException(OrderProblem.Capacity);
            var receipt=new RevisionReceipt(Guid.NewGuid().ToString("N"),orderId,command.OperationKey,order.Version,checked(order.Version+1),note,clock.GetUtcNow());
            using(var insert=Command("INSERT INTO commerce_revisions VALUES($id,$order,$key,$base,$applied,$note,$at)",tx,("$id",receipt.Id),("$order",orderId),("$key",receipt.OperationKey),("$base",receipt.BaseVersion),("$applied",receipt.AppliedVersion),("$note",note),("$at",receipt.At.UtcTicks)))insert.ExecuteNonQuery();
            SetState(order,OrderState.InProduction,tx);Notice(orderId,receipt.AppliedVersion,OrderNoticeKind.RevisionRequested,tx);tx.Commit();return receipt;
        }
    }
    private static DeliveryDescriptor Descriptor(string id,string orderId,long version,bool revoked)=>new(id,orderId,version,PrivateDeliveryFixture.Length,PrivateDeliveryFixture.Sha256,"audio/wav",revoked);
    private DeliveryDescriptor? Delivery(string orderId,string deliveryId,SqliteTransaction? tx)
    {using var c=Command("SELECT id,version,revoked FROM commerce_deliveries WHERE order_id=$id AND id=$delivery",tx,("$id",orderId),("$delivery",deliveryId));using var r=c.ExecuteReader();return !r.Read()?null:Descriptor(r.GetString(0),orderId,r.GetInt64(1),r.GetInt64(2)!=0);}
    public OrderTracking TrackOrder(OrderOwner owner,string id)
    {
        lock(sync)
        {
            using var tx=db.BeginTransaction(deferred:true);var order=Owned(owner,id,tx);var history=new List<OrderHistory>();var revisions=new List<RevisionReceipt>();var deliveries=new List<DeliveryDescriptor>();var notices=new List<OrderNotice>();
            using(var c=Command("SELECT version,state,at_ticks FROM commerce_history WHERE order_id=$id ORDER BY version LIMIT 101",tx,("$id",id)))using(var r=c.ExecuteReader())while(r.Read())history.Add(new(r.GetInt64(0),Enum.Parse<OrderState>(r.GetString(1)),new(r.GetInt64(2),TimeSpan.Zero)));
            using(var c=Command("SELECT id,operation_key,base_version,applied_version,note,at_ticks FROM commerce_revisions WHERE order_id=$id ORDER BY applied_version LIMIT 11",tx,("$id",id)))using(var r=c.ExecuteReader())while(r.Read())revisions.Add(ReadRevision(r,id));
            using(var c=Command("SELECT id,version,revoked FROM commerce_deliveries WHERE order_id=$id",tx,("$id",id)))using(var r=c.ExecuteReader())while(r.Read())deliveries.Add(Descriptor(r.GetString(0),id,r.GetInt64(1),r.GetInt64(2)!=0));
            using(var c=Command("SELECT id,version,kind,at_ticks FROM commerce_notices WHERE order_id=$id ORDER BY version LIMIT 101",tx,("$id",id)))using(var r=c.ExecuteReader())while(r.Read())notices.Add(new(r.GetString(0),r.GetInt64(1),Enum.Parse<OrderNoticeKind>(r.GetString(2)),new(r.GetInt64(3),TimeSpan.Zero)));
            if(history.Count>100||revisions.Count>10||notices.Count>100)throw new OrderRejectedException(OrderProblem.Capacity);tx.Commit();return new(order,history,revisions,deliveries,notices);
        }
    }
    /// <summary>Trusted test fixture, no admin/customer publication endpoint.</summary>
    public OrderSnapshot PublishReviewForLab(string id,long expectedVersion)
    {
        lock(sync){using var tx=db.BeginTransaction(deferred:false);var order=FindOrder("id=$id",tx,("$id",id))??throw new OrderRejectedException(OrderProblem.Unavailable);
            if(order.Version!=expectedVersion)throw new OrderRejectedException(OrderProblem.VersionConflict);
            if(order.State!=OrderState.InProduction||Payment(id,tx)?.State!=TestPaymentState.Paid)throw new OrderRejectedException(OrderProblem.InvalidTransition);
            SetState(order,OrderState.ReadyForReview,tx);Notice(id,order.Version+1,OrderNoticeKind.ReadyForReview,tx);tx.Commit();return order with{State=OrderState.ReadyForReview,Version=order.Version+1};}
    }
    public DeliveryDescriptor PublishDeliveryForLab(string id,long expectedVersion)
    {
        lock(sync){using var tx=db.BeginTransaction(deferred:false);var order=FindOrder("id=$id",tx,("$id",id))??throw new OrderRejectedException(OrderProblem.Unavailable);
            using(var existing=Command("SELECT id,version,revoked FROM commerce_deliveries WHERE order_id=$id",tx,("$id",id)))using(var r=existing.ExecuteReader())if(r.Read()){if(r.GetInt64(1)!=expectedVersion+1)throw new OrderRejectedException(OrderProblem.KeyConflict);var descriptor=Descriptor(r.GetString(0),id,r.GetInt64(1),r.GetInt64(2)!=0);tx.Commit();return descriptor;}
            if(order.Version!=expectedVersion)throw new OrderRejectedException(OrderProblem.VersionConflict);
            if(order.State!=OrderState.ReadyForReview||Payment(id,tx)?.State!=TestPaymentState.Paid)throw new OrderRejectedException(OrderProblem.InvalidTransition);
            var delivery=Descriptor(Guid.NewGuid().ToString("N"),id,checked(order.Version+1),false);
            using(var c=Command("INSERT INTO commerce_deliveries VALUES($delivery,$id,$version,0)",tx,("$delivery",delivery.Id),("$id",id),("$version",delivery.Version)))c.ExecuteNonQuery();
            SetState(order,OrderState.Delivered,tx);Notice(id,delivery.Version,OrderNoticeKind.Delivered,tx);tx.Commit();return delivery;}
    }
    public void RevokeDeliveryForLab(string orderId,string deliveryId,long expectedVersion)
    {
        lock(sync){using var tx=db.BeginTransaction(deferred:false);var order=FindOrder("id=$id",tx,("$id",orderId))??throw new OrderRejectedException(OrderProblem.Unavailable);var d=Delivery(orderId,deliveryId,tx)??throw new OrderRejectedException(OrderProblem.Unavailable);
            if(order.Version!=expectedVersion)throw new OrderRejectedException(OrderProblem.VersionConflict);if(d.Revoked){tx.Commit();return;}
            using(var c=Command("UPDATE commerce_deliveries SET revoked=1 WHERE id=$id",tx,("$id",deliveryId)))c.ExecuteNonQuery();
            using(var c=Command("DELETE FROM commerce_delivery_grants WHERE delivery_id=$id",tx,("$id",deliveryId)))c.ExecuteNonQuery();
            SetState(order,order.State,tx);Notice(orderId,order.Version+1,OrderNoticeKind.DeliveryRevoked,tx);tx.Commit();}
    }
    public DeliveryGrant IssueDeliveryGrant(OrderOwner owner,string orderId,string deliveryId,long expectedVersion)
    {
        if(!OrderContract.Id(deliveryId)||expectedVersion<=0)throw new OrderRejectedException(OrderProblem.InvalidRequest);
        lock(sync){using var tx=db.BeginTransaction(deferred:false);var order=Owned(owner,orderId,tx);var d=Delivery(orderId,deliveryId,tx)??throw new OrderRejectedException(OrderProblem.Unavailable);
            if(d.Revoked||order.State!=OrderState.Delivered||Payment(orderId,tx)?.State!=TestPaymentState.Paid)throw new OrderRejectedException(OrderProblem.Unavailable);
            if(order.Version!=expectedVersion||d.Version!=order.Version)throw new OrderRejectedException(OrderProblem.VersionConflict);
            using(var cleanup=Command("DELETE FROM commerce_delivery_grants WHERE expires_ticks<=$now",tx,("$now",clock.GetUtcNow().UtcTicks)))cleanup.ExecuteNonQuery();
            using(var count=Command("SELECT COUNT(*),COALESCE(SUM(CASE WHEN delivery_id=$id THEN 1 ELSE 0 END),0) FROM commerce_delivery_grants",tx,("$id",deliveryId)))using(var r=count.ExecuteReader()){r.Read();if(r.GetInt64(0)>=1000||r.GetInt64(1)>=4)throw new OrderRejectedException(OrderProblem.Capacity);}
            var token=Convert.ToHexString(RandomNumberGenerator.GetBytes(32)).ToLowerInvariant();var expiry=clock.GetUtcNow().AddMinutes(2);
            using(var insert=Command("INSERT INTO commerce_delivery_grants VALUES($hash,$delivery,$i,$s,$version,$expires)",tx,("$hash",GrantHash(token)),("$delivery",deliveryId),("$i",owner.Issuer),("$s",owner.Subject),("$version",order.Version),("$expires",expiry.UtcTicks)))insert.ExecuteNonQuery();tx.Commit();return new(token,d,expiry);}
    }
    private static string GrantHash(string token)=>Convert.ToHexString(SHA256.HashData(Encoding.ASCII.GetBytes(token)));
    public DeliveryDescriptor AuthorizeDelivery(OrderOwner owner,string orderId,string deliveryId,string token)
    {
        if(!TrackingContract.GrantToken(token)||!OrderContract.Id(deliveryId))throw new OrderRejectedException(OrderProblem.Unavailable);
        lock(sync){using var tx=db.BeginTransaction(deferred:true);var order=Owned(owner,orderId,tx);var d=Delivery(orderId,deliveryId,tx)??throw new OrderRejectedException(OrderProblem.Unavailable);
            if(d.Revoked||order.State!=OrderState.Delivered||d.Version!=order.Version||Payment(orderId,tx)?.State!=TestPaymentState.Paid)throw new OrderRejectedException(OrderProblem.Unavailable);
            using var c=Command("SELECT COUNT(*) FROM commerce_delivery_grants WHERE token_hash=$hash AND delivery_id=$delivery AND issuer=$i AND subject=$s AND order_version=$version AND expires_ticks>$now",tx,("$hash",GrantHash(token)),("$delivery",deliveryId),("$i",owner.Issuer),("$s",owner.Subject),("$version",order.Version),("$now",clock.GetUtcNow().UtcTicks));
            if((long)c.ExecuteScalar()!!=1)throw new OrderRejectedException(OrderProblem.Unavailable);tx.Commit();return d;}
    }
}

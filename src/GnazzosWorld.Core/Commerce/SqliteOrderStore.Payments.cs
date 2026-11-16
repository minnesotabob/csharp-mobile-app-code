using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using GnazzosWorld.Core.Payments;
using Microsoft.Data.Sqlite;

namespace GnazzosWorld.Core.Commerce;

public sealed partial class SqliteOrderStore
{
    private void InitializePayments()
    {
        using var c=db.CreateCommand();c.CommandText="""
            CREATE TABLE IF NOT EXISTS commerce_payments (
              order_id TEXT PRIMARY KEY REFERENCES commerce_orders(id), attempt_id TEXT NOT NULL UNIQUE,
              scope TEXT NOT NULL, total_minor INTEGER NOT NULL, currency TEXT NOT NULL, created_ticks INTEGER NOT NULL,
              return_origin TEXT NOT NULL, session_id TEXT UNIQUE, checkout_url TEXT, state TEXT NOT NULL,
              version INTEGER NOT NULL, payment_intent TEXT);
            CREATE TABLE IF NOT EXISTS commerce_payment_events (
              scope TEXT NOT NULL, event_id TEXT NOT NULL, fingerprint TEXT NOT NULL,
              attempt_id TEXT NOT NULL REFERENCES commerce_payments(attempt_id), PRIMARY KEY(scope,event_id));
            """;c.ExecuteNonQuery();
    }
    public CheckoutReservation ReserveTestCheckout(OrderOwner owner,string orderId,string scope,string returnOrigin)
    {
        owner.Validate();if(!OrderContract.Id(orderId)||!scope.StartsWith("stripe-test:acct_",StringComparison.Ordinal)||scope.Length>160||!PaymentContract.ReturnOrigin(returnOrigin))throw new PaymentConflictException();
        lock(sync)
        {
            using var tx=db.BeginTransaction(deferred:false);var order=FindOrder("id=$id AND issuer=$i AND subject=$s",tx,("$id",orderId),("$i",owner.Issuer),("$s",owner.Subject))??throw new OrderRejectedException(OrderProblem.Unavailable);
            var old=Reservation(orderId,tx);
            if(old is not null){if(old.Scope!=scope||old.ReturnOrigin!=returnOrigin)throw new PaymentConflictException();tx.Commit();return old;}
            if(order.State!=OrderState.Accepted)throw new PaymentConflictException();
            var reservation=new CheckoutReservation(Guid.NewGuid().ToString("N"),order.Id,scope,order.TotalMinor,order.Currency,clock.GetUtcNow(),returnOrigin);
            using var c=Command("INSERT INTO commerce_payments VALUES($order,$attempt,$scope,$total,$currency,$created,$origin,NULL,NULL,'Creating',1,NULL)",tx,
                ("$order",orderId),("$attempt",reservation.AttemptId),("$scope",scope),("$total",order.TotalMinor),("$currency",order.Currency),("$created",reservation.CreatedAt.UtcTicks),("$origin",returnOrigin));c.ExecuteNonQuery();tx.Commit();return reservation;
        }
    }
    private CheckoutReservation? Reservation(string orderId,SqliteTransaction? tx)
    {
        using var c=Command("SELECT attempt_id,order_id,scope,total_minor,currency,created_ticks,return_origin FROM commerce_payments WHERE order_id=$id",tx,("$id",orderId));using var r=c.ExecuteReader();
        return !r.Read()?null:new(r.GetString(0),r.GetString(1),r.GetString(2),r.GetInt64(3),r.GetString(4),new(r.GetInt64(5),TimeSpan.Zero),r.GetString(6));
    }
    private TestPaymentReceipt? Payment(string orderId,SqliteTransaction? tx=null)
    {
        using var c=Command("SELECT attempt_id,state,version,session_id,checkout_url FROM commerce_payments WHERE order_id=$id",tx,("$id",orderId));using var r=c.ExecuteReader();if(!r.Read())return null;
        var state=Enum.Parse<TestPaymentState>(r.GetString(1));return new(orderId,r.GetString(0),state,r.GetInt64(2),r.IsDBNull(3)?null:r.GetString(3),state==TestPaymentState.Pending&&!r.IsDBNull(4)?r.GetString(4):null);
    }
    public TestPaymentReceipt? GetTestPayment(OrderOwner owner,string orderId)
    {
        owner.Validate();if(!OrderContract.Id(orderId))throw new OrderRejectedException(OrderProblem.InvalidRequest);
        lock(sync){if(FindOrder("id=$id AND issuer=$i AND subject=$s",null,("$id",orderId),("$i",owner.Issuer),("$s",owner.Subject)) is null)throw new OrderRejectedException(OrderProblem.Unavailable);return Payment(orderId);}
    }
    public TestPaymentReceipt AttachTestSession(CheckoutReservation reservation,HostedTestSession session)
    {
        if(!PaymentContract.ProviderId(session.Id,"cs_test_")||!PaymentContract.CheckoutUrl(session.Url))throw new PaymentConflictException();
        lock(sync)
        {
            using var tx=db.BeginTransaction(deferred:false);if(Reservation(reservation.OrderId,tx)!=reservation)throw new PaymentConflictException();var current=Payment(reservation.OrderId,tx)!;
            if(current.SessionId is not null){if(current.SessionId!=session.Id)throw new PaymentConflictException();tx.Commit();return current;}
            using var c=Command("UPDATE commerce_payments SET session_id=$session,checkout_url=$url,state='Pending',version=version+1 WHERE order_id=$id",tx,("$session",session.Id),("$url",session.Url),("$id",reservation.OrderId));c.ExecuteNonQuery();tx.Commit();return Payment(reservation.OrderId)!;
        }
    }
    /// <summary>Trusted verifier input only. Event receipt and state commit together. No browser return can call this.</summary>
    public TestPaymentReceipt ApplyVerifiedTestEvent(VerifiedCheckoutEvent e)
    {
        var fingerprint=Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(JsonSerializer.Serialize(e,OrderContract.Json))));
        lock(sync)
        {
            using var tx=db.BeginTransaction(deferred:false);
            using(var seen=Command("SELECT fingerprint FROM commerce_payment_events WHERE scope=$scope AND event_id=$event",tx,("$scope",e.Scope),("$event",e.EventId)))
            {if(seen.ExecuteScalar() is string prior){if(prior!=fingerprint)throw new PaymentConflictException();var duplicate=Payment(e.OrderId,tx)??throw new PaymentConflictException();tx.Commit();return duplicate;}}
            var reservation=Reservation(e.OrderId,tx)??throw new PaymentConflictException();var payment=Payment(e.OrderId,tx)!;
            if(reservation.Scope!=e.Scope||reservation.AttemptId!=e.AttemptId||reservation.TotalMinor!=e.TotalMinor||reservation.Currency!=e.Currency||payment.SessionId!=e.SessionId)throw new PaymentConflictException();
            using(var count=Command("SELECT COUNT(*) FROM commerce_payment_events",tx))if((long)count.ExecuteScalar()!>=10000)throw new PaymentConflictException();
            using(var insert=Command("INSERT INTO commerce_payment_events VALUES($scope,$event,$hash,$attempt)",tx,("$scope",e.Scope),("$event",e.EventId),("$hash",fingerprint),("$attempt",e.AttemptId)))insert.ExecuteNonQuery();
            var order=FindOrder("id=$id",tx,("$id",e.OrderId))!;
            // Paid evidence never regresses on late failure/expiry. Paid after cancellation requires human review.
            var target=payment.State is TestPaymentState.Paid or TestPaymentState.ReviewRequired?payment.State:
                e.State==TestPaymentState.Paid?(order.State==OrderState.Canceled?TestPaymentState.ReviewRequired:TestPaymentState.Paid):
                payment.State is TestPaymentState.Failed or TestPaymentState.Expired?payment.State:e.State;
            if(target!=payment.State)
            {
                using var update=Command("UPDATE commerce_payments SET state=$state,version=version+1,payment_intent=COALESCE($intent,payment_intent) WHERE order_id=$id",tx,
                    ("$state",target.ToString()),("$intent",(object?)e.PaymentIntent??DBNull.Value),("$id",e.OrderId));update.ExecuteNonQuery();
            }
            tx.Commit();return Payment(e.OrderId)!;
        }
    }
    /// <summary>Controlled test production gate; does not authorize real work or expose an admin route.</summary>
    public OrderSnapshot StartProductionForPaymentLab(string orderId,long expectedVersion)
    {
        lock(sync)
        {
            using var tx=db.BeginTransaction(deferred:false);var order=FindOrder("id=$id",tx,("$id",orderId))??throw new PaymentConflictException();
            if(order.State!=OrderState.Accepted||order.Version!=expectedVersion||Payment(orderId,tx)?.State!=TestPaymentState.Paid)throw new PaymentConflictException();
            using var c=Command("UPDATE commerce_orders SET state='InProduction',version=version+1 WHERE id=$id AND version=$version",tx,("$id",orderId),("$version",expectedVersion));if(c.ExecuteNonQuery()!=1)throw new PaymentConflictException();
            AddHistory(orderId,order.Version+1,OrderState.InProduction,tx);tx.Commit();return order with{State=OrderState.InProduction,Version=order.Version+1};
        }
    }
}

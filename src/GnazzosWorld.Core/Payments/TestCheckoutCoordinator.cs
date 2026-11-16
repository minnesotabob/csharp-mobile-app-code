using GnazzosWorld.Core.Commerce;

namespace GnazzosWorld.Core.Payments;

public sealed class TestCheckoutCoordinator(SqliteOrderStore store,ITestCheckoutProvider provider,TimeProvider clock)
{
    public async Task<TestPaymentReceipt> CreateAsync(OrderOwner owner,string orderId,CancellationToken ct)
    {
        ct.ThrowIfCancellationRequested();
        var reservation=store.ReserveTestCheckout(owner,orderId,provider.Scope,provider.ReturnOrigin);
        var current=store.GetTestPayment(owner,orderId)!;
        if(current.State!=TestPaymentState.Creating)return current;
        // Provider keys may be pruned after 24h. Never automatically POST an unresolved key then.
        if(clock.GetUtcNow()<reservation.CreatedAt||clock.GetUtcNow()-reservation.CreatedAt>=TimeSpan.FromHours(23))throw new PaymentConflictException();
        var session=await provider.CreateAsync(reservation,ct);ct.ThrowIfCancellationRequested();return store.AttachTestSession(reservation,session);
    }
}

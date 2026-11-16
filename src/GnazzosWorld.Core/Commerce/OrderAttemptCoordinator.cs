using GnazzosWorld.Core.Identity;

namespace GnazzosWorld.Core.Commerce;

public sealed class OrderAttemptCoordinator(ApiSession session,OrderApiClient api,PendingOrderDatabase pending)
{
    public CommerceAccount Account(OwnedAccessLease lease)
    {
        if(!session.IsCurrent(lease.Access))throw new SessionEndedException();
        return new(new(lease.Issuer.AbsoluteUri,lease.Subject),session.Settings.ApiOrigin.AbsoluteUri,session.Settings.Audience);
    }
    public Task<OrderSnapshot> AcceptAsync(OrderQuote quote,OwnedAccessLease lease,CancellationToken ct=default)
    {
        ct.ThrowIfCancellationRequested();var account=Account(lease);pending.Begin(account,quote); // Persist before any acceptance HTTP call.
        return ResumeAsync(lease,ct);
    }
    public async Task<OrderSnapshot> ResumeAsync(OwnedAccessLease lease,CancellationToken ct=default)
    {
        var account=Account(lease);var operation=pending.Read(account)??throw new InvalidOperationException("No saved acceptance.");
        if(operation.Rejected is not null)throw new OrderRejectedException(operation.Rejected.Value);
        OrderSnapshot order;
        try
        {
            order=operation.Confirmed is null ? await api.AcceptAsync(operation.Quote,operation.OperationKey,lease,ct)
                : await api.GetAsync(operation.Confirmed.Id,lease,ct);
        }
        catch(OrderApiException ex) when(operation.Confirmed is null&&PendingOrderDatabase.Definitive(ex.Problem))
        {
            ct.ThrowIfCancellationRequested();_ = Account(lease);pending.Reject(account,operation.OperationKey,ex.Problem!.Value);throw;
        }
        ct.ThrowIfCancellationRequested();_ = Account(lease);OrderApiClient.Validate(order,operation.Quote,operation.OperationKey);
        pending.Confirm(account,operation.OperationKey,order);return order;
    }
}

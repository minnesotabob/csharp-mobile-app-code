using GnazzosWorld.Core.Commerce;
using GnazzosWorld.Core.Identity;

namespace GnazzosWorld.Core.Fulfillment;

public sealed class RevisionCoordinator(TrackingApiClient api,PendingRevisionDatabase journal)
{
    public Task<RevisionReceipt> SubmitAsync(string orderId,long version,string note,OwnedAccessLease lease,CancellationToken ct)
    {ct.ThrowIfCancellationRequested();journal.Begin(api.Account(lease),orderId,version,note);return ResumeAsync(orderId,lease,ct);}
    public async Task<RevisionReceipt> ResumeAsync(string orderId,OwnedAccessLease lease,CancellationToken ct)
    {
        ct.ThrowIfCancellationRequested();var account=api.Account(lease);var pending=journal.Read(account,orderId)??throw new InvalidOperationException("No saved revision.");
        if(pending.Confirmed is not null){ct.ThrowIfCancellationRequested();_=api.Account(lease);return pending.Confirmed;}if(pending.Rejected is not null)throw new OrderRejectedException(pending.Rejected.Value);
        try{var receipt=await api.ReviseAsync(orderId,pending.Command,lease,ct);ct.ThrowIfCancellationRequested();_=api.Account(lease);journal.Finish(account,orderId,pending.Command.OperationKey,receipt);return receipt;}
        catch(TrackingApiException ex) when(PendingRevisionDatabase.Definitive(ex.Problem))
        {ct.ThrowIfCancellationRequested();_=api.Account(lease);journal.Finish(account,orderId,pending.Command.OperationKey,null,ex.Problem);throw;}
    }
}

using GnazzosWorld.Core.Commerce;

namespace GnazzosWorld.Core.Fulfillment;

public sealed record OrderSummary(string Id,OrderState State,long Version,DateTimeOffset CreatedAt);
public sealed record OrderPage(IReadOnlyList<OrderSummary> Items,string? Next);
public enum OrderNoticeKind { ReadyForReview, RevisionRequested, Delivered, DeliveryRevoked }
public sealed record OrderNotice(string Id,long Version,OrderNoticeKind Kind,DateTimeOffset At);
public sealed record RevisionCommand(long ExpectedVersion,string OperationKey,string Note)
{public override string ToString()=>"Revision command [private note omitted]";}
public sealed record RevisionReceipt(string Id,string OrderId,string OperationKey,long BaseVersion,long AppliedVersion,string Note,DateTimeOffset At)
{public override string ToString()=>"Revision receipt [private note omitted]";}
public sealed record DeliveryDescriptor(string Id,string OrderId,long Version,long Length,string Sha256,string MediaType,bool Revoked);
public sealed record OrderTracking(OrderSnapshot Order,IReadOnlyList<OrderHistory> History,IReadOnlyList<RevisionReceipt> Revisions,IReadOnlyList<DeliveryDescriptor> Deliveries,IReadOnlyList<OrderNotice> Notices)
{public override string ToString()=>"Order tracking [private request omitted]";}
public sealed record DeliveryGrant(string Token,DeliveryDescriptor Delivery,DateTimeOffset ExpiresAt)
{public override string ToString()=>"Delivery grant [credential omitted]";}
public static class TrackingContract
{
    public static string Note(string? note)
    {
        if(note is null||string.IsNullOrWhiteSpace(note)||note.Length>1000||note.Any(c=>char.IsControl(c)&&c is not '\r' and not '\n' and not '\t'))throw new OrderRejectedException(OrderProblem.InvalidRequest);
        return note.Trim();
    }
    public static bool GrantToken(string? token)=>token is not null&&token.Length==64&&token.All(c=>c is >= '0' and <= '9' or >= 'a' and <= 'f');
    public static void Descriptor(DeliveryDescriptor d,string orderId)
    {
        if(!OrderContract.Id(d.Id)||d.OrderId!=orderId||!OrderContract.Id(orderId)||d.Version<=0||d.Length!=PrivateDeliveryFixture.Length||d.Sha256!=PrivateDeliveryFixture.Sha256||d.MediaType!="audio/wav")throw new InvalidDataException("Invalid lab delivery descriptor.");
    }
    public static void Revision(RevisionReceipt r,string orderId,RevisionCommand command)
    {
        if(!OrderContract.Id(r.Id)||r.OrderId!=orderId||r.OperationKey!=command.OperationKey||r.BaseVersion!=command.ExpectedVersion||r.AppliedVersion!=checked(r.BaseVersion+1)||r.Note!=Note(command.Note))throw new InvalidDataException("Revision receipt does not match saved operation.");
    }
}

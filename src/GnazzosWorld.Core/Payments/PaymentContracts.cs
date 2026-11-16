using System.Text.Json;
using GnazzosWorld.Core.Commerce;

namespace GnazzosWorld.Core.Payments;

public enum TestPaymentState { Creating, Pending, Failed, Expired, Paid, ReviewRequired }
public sealed record TestPaymentReceipt(string OrderId,string AttemptId,TestPaymentState State,long Version,string? SessionId,string? CheckoutUrl)
{ public override string ToString()=>"Test payment receipt [private identifiers omitted]"; }
public sealed record CheckoutReservation(string AttemptId,string OrderId,string Scope,long TotalMinor,string Currency,DateTimeOffset CreatedAt,string ReturnOrigin)
{ public override string ToString()=>"Test checkout reservation [private identifiers omitted]"; }
public sealed record HostedTestSession(string Id,string Url)
{ public override string ToString()=>"Test checkout session [private URL omitted]"; }
public sealed class PaymentConflictException:Exception
{public PaymentConflictException():base("The test payment needs reconciliation."){} }
public interface ITestCheckoutProvider
{
    string Scope {get;}
    string ReturnOrigin {get;}
    Task<HostedTestSession> CreateAsync(CheckoutReservation reservation,CancellationToken ct);
}
public sealed class VerifiedCheckoutEvent
{
    internal VerifiedCheckoutEvent(string eventId,string sessionId,string attemptId,string orderId,string scope,long total,string currency,TestPaymentState state,string? paymentIntent)
    {EventId=eventId;SessionId=sessionId;AttemptId=attemptId;OrderId=orderId;Scope=scope;TotalMinor=total;Currency=currency;State=state;PaymentIntent=paymentIntent;}
    public string EventId{get;} public string SessionId{get;} public string AttemptId{get;} public string OrderId{get;} public string Scope{get;}
    public long TotalMinor{get;} public string Currency{get;} public TestPaymentState State{get;} public string? PaymentIntent{get;}
    public override string ToString()=>"Verified test event [private identifiers omitted]";
}
public static class PaymentContract
{
    public static bool ProviderId(string? id,string prefix)=>id is not null && id.StartsWith(prefix,StringComparison.Ordinal)&&id.Length>prefix.Length&&id.Length<=128&&id.All(c=>char.IsAsciiLetterOrDigit(c)||c=='_');
    public static bool CheckoutUrl(string? raw)=>Uri.TryCreate(raw,UriKind.Absolute,out var uri)&&uri.Scheme=="https"&&uri.Host=="checkout.stripe.com"&&uri.Port==443&&uri.UserInfo.Length==0&&raw.Length<=4096;
    public static bool ReturnOrigin(string raw)=>Uri.TryCreate(raw,UriKind.Absolute,out var uri)&&uri.Scheme=="https"&&uri.UserInfo.Length==0&&uri.AbsolutePath=="/"&&uri.Query.Length==0&&uri.Fragment.Length==0&&raw.Length<=256;
    public static void Validate(TestPaymentReceipt receipt,string orderId)
    {
        if(receipt.OrderId!=orderId||!OrderContract.Id(orderId)||!OrderContract.Id(receipt.AttemptId)||!Enum.IsDefined(receipt.State)||receipt.Version<1||
           (receipt.SessionId is not null&&!ProviderId(receipt.SessionId,"cs_test_"))||(receipt.CheckoutUrl is not null&&(!CheckoutUrl(receipt.CheckoutUrl)||receipt.State!=TestPaymentState.Pending)))throw new InvalidDataException("Invalid test payment receipt.");
    }
    internal static void Unique(JsonElement e)
    {
        if(e.ValueKind==JsonValueKind.Object){var properties=e.EnumerateObject().ToArray();if(properties.Select(p=>p.Name).Distinct(StringComparer.OrdinalIgnoreCase).Count()!=properties.Length)throw new InvalidDataException("Duplicate payment JSON field.");foreach(var p in properties)Unique(p.Value);}
        else if(e.ValueKind==JsonValueKind.Array)foreach(var item in e.EnumerateArray())Unique(item);
    }
}

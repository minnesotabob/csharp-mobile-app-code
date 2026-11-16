using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using GnazzosWorld.Core.Orders;

namespace GnazzosWorld.Core.Commerce;

public sealed record OrderOwner(string Issuer, string Subject)
{
    public void Validate()
    {
        if (!Uri.TryCreate(Issuer, UriKind.Absolute, out var uri) || uri.Scheme != "https" || uri.UserInfo.Length != 0 || Issuer.Length > 256 ||
            string.IsNullOrWhiteSpace(Subject) || Subject.Length > 256 || Subject.Any(char.IsControl)) throw new InvalidDataException("Invalid order owner.");
    }
    public override string ToString() => "Order owner [identity omitted]";
}
public enum OrderState { Accepted, InProduction, ReadyForReview, Delivered, Canceled }
public enum OrderProblem { InvalidRequest, Unavailable, ExpiredQuote, ChangedRequest, KeyConflict, QuoteUsed, VersionConflict, InvalidTransition, Capacity }
public sealed class OrderRejectedException(OrderProblem problem) : Exception("Order operation was not accepted.")
{ public OrderProblem Problem { get; } = problem; }
public sealed record QuoteCommand(SongRequest Request)
{ public override string ToString() => "Quote command [private request omitted]"; }
public sealed record AcceptOrderCommand(string QuoteId, string OperationKey, SongRequest Request)
{ public override string ToString() => "Order command [private request omitted]"; }
public sealed record OrderQuote(string Id, SongRequest Request, string RequestHash, string PricingVersion, string Currency, long TotalMinor, DateTimeOffset ExpiresAt)
{ public override string ToString() => "Owned quote [private request omitted]"; }
public sealed record OrderSnapshot(string Id, string QuoteId, string OperationKey, SongRequest Request, string RequestHash,
    string PricingVersion, string Currency, long TotalMinor, OrderState State, long Version, DateTimeOffset CreatedAt)
{ public override string ToString() => "Owned order [private request omitted]"; }
public sealed record OrderHistory(long Version, OrderState State, DateTimeOffset At);
public static class OrderContract
{
    public static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web)
    { UnmappedMemberHandling = JsonUnmappedMemberHandling.Disallow, Converters = { new JsonStringEnumConverter() } };
    public static bool Id(string? value) => Guid.TryParseExact(value, "N", out var id) && id.ToString("N") == value;
    public static SongRequest Normalize(SongRequest? request)
    {
        if (request is null || request.Genre is null || request.Occasion is null || request.Story is null || request.ContactName is null || request.Email is null || request.VocalNotes is null)
            throw new OrderRejectedException(OrderProblem.InvalidRequest);
        var fields = new[] { request.Genre, request.Occasion, request.Story, request.ContactName, request.Email, request.VocalNotes };
        if (fields.Any(s => s.Any(c => char.IsControl(c) && c is not '\r' and not '\n' and not '\t')) ||
            SongRequestRules.Validate(request, 2).Count != 0) throw new OrderRejectedException(OrderProblem.InvalidRequest);
        return request.ForReview();
    }
    public static string Fingerprint(SongRequest request) => Convert.ToHexString(SHA256.HashData(
        Encoding.UTF8.GetBytes("request-v1\n" + JsonSerializer.Serialize(Normalize(request), Json)))).ToLowerInvariant();
    public static bool CanTransition(OrderState from, OrderState to) => (from, to) is
        (OrderState.Accepted, OrderState.InProduction) or (OrderState.Accepted, OrderState.Canceled) or
        (OrderState.InProduction, OrderState.ReadyForReview) or (OrderState.ReadyForReview, OrderState.InProduction) or
        (OrderState.ReadyForReview, OrderState.Delivered);
}
public interface IOrderPricing
{
    string Version { get; }
    long TotalMinor(SongRequest request);
}
public sealed class WebsiteSnapshotOrderPricing : IOrderPricing
{
    public string Version => "lab-" + SongPricing.Version;
    public long TotalMinor(SongRequest request) => checked((long)(SongPricing.Calculate(request.Minutes, request.WithVocals, request.Quantity) * 100));
}

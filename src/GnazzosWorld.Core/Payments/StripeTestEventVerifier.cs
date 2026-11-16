using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using GnazzosWorld.Core.Commerce;

namespace GnazzosWorld.Core.Payments;

/// <summary>Restricted direct-account snapshot-event verifier. Test mode only; not a general Stripe SDK.</summary>
public sealed class StripeTestEventVerifier
{
    private readonly byte[] secret;private readonly string scope;private readonly TimeProvider clock;
    public StripeTestEventVerifier(string secret,string scope,TimeProvider clock)
    {
        if(!secret.StartsWith("whsec_",StringComparison.Ordinal)||secret.Length<24||secret.Length>256||!scope.StartsWith("stripe-test:acct_",StringComparison.Ordinal))throw new ArgumentException("Configure a test webhook secret and account scope.");
        this.secret=Encoding.UTF8.GetBytes(secret);this.scope=scope;this.clock=clock;
    }
    public VerifiedCheckoutEvent? Verify(ReadOnlySpan<byte> raw,string header)
    {
        if(raw.Length is 0 or >65536||header.Length>2048)throw new InvalidDataException("Invalid webhook envelope.");
        var pieces=header.Split(',');var timestamps=pieces.Where(p=>p.StartsWith("t=",StringComparison.Ordinal)).ToArray();
        if(timestamps.Length!=1||!long.TryParse(timestamps[0].AsSpan(2),NumberStyles.None,CultureInfo.InvariantCulture,out var timestamp)||Math.Abs((decimal)clock.GetUtcNow().ToUnixTimeSeconds()-timestamp)>300)throw new InvalidDataException("Webhook timestamp rejected.");
        var prefix=Encoding.ASCII.GetBytes(timestamp.ToString(CultureInfo.InvariantCulture)+".");var material=new byte[prefix.Length+raw.Length];prefix.CopyTo(material,0);raw.CopyTo(material.AsSpan(prefix.Length));
        var expected=HMACSHA256.HashData(secret,material);var matched=false;
        foreach(var item in pieces.Where(p=>p.StartsWith("v1=",StringComparison.Ordinal)))
        {if(item.Length!=67)continue;try{var supplied=Convert.FromHexString(item[3..]);matched|=CryptographicOperations.FixedTimeEquals(expected,supplied);}catch(FormatException){ }}
        if(!matched)throw new InvalidDataException("Webhook signature rejected.");
        try
        {
            using var doc=JsonDocument.Parse(raw.ToArray(),new JsonDocumentOptions{MaxDepth=16});var e=doc.RootElement;PaymentContract.Unique(e);
            if(e.GetProperty("object").GetString()!="event"||e.GetProperty("livemode").GetBoolean()||(e.TryGetProperty("account",out var account)&&account.ValueKind!=JsonValueKind.Null)||!PaymentContract.ProviderId(e.GetProperty("id").GetString(),"evt_"))throw new InvalidDataException("Unsupported event environment.");
            var type=e.GetProperty("type").GetString();
            if(type is not ("checkout.session.completed" or "checkout.session.async_payment_succeeded" or "checkout.session.async_payment_failed" or "checkout.session.expired"))return null;
            var s=e.GetProperty("data").GetProperty("object");
            var id=s.GetProperty("id").GetString();var attempt=s.GetProperty("client_reference_id").GetString();var order=s.GetProperty("metadata").GetProperty("order_id").GetString();
            var currency=s.GetProperty("currency").GetString();var total=s.GetProperty("amount_total").GetInt64();var payment=s.GetProperty("payment_status").GetString();
            var intent=s.TryGetProperty("payment_intent",out var pi)&&pi.ValueKind==JsonValueKind.String?pi.GetString():null;
            if(s.GetProperty("object").GetString()!="checkout.session"||s.GetProperty("mode").GetString()!="payment"||s.GetProperty("livemode").GetBoolean()||!PaymentContract.ProviderId(id,"cs_test_")||!OrderContract.Id(attempt)||!OrderContract.Id(order)||currency!="usd"||total is <=0 or >1000000)throw new InvalidDataException("Unsupported checkout object.");
            var state=type switch
            {
                "checkout.session.async_payment_succeeded" when payment=="paid"=>TestPaymentState.Paid,
                "checkout.session.completed" when payment=="paid"=>TestPaymentState.Paid,
                "checkout.session.completed" when payment=="unpaid"=>TestPaymentState.Pending,
                "checkout.session.async_payment_failed" when payment=="unpaid"=>TestPaymentState.Failed,
                "checkout.session.expired" when payment=="unpaid"=>TestPaymentState.Expired,
                _=>throw new InvalidDataException("Inconsistent checkout event.")
            };
            if(state==TestPaymentState.Paid&&!PaymentContract.ProviderId(intent,"pi_"))throw new InvalidDataException("Paid checkout lacks payment reference.");
            return new(e.GetProperty("id").GetString()!,id!,attempt!,order!,scope,total,"USD",state,intent);
        }
        catch(Exception ex) when(ex is JsonException or KeyNotFoundException or InvalidOperationException or FormatException or OverflowException){throw new InvalidDataException("Invalid payment event.",ex);}
    }
}

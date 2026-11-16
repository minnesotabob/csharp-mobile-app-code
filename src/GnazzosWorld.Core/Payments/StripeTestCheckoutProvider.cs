using System.Net.Http.Headers;
using System.Text.Json;

namespace GnazzosWorld.Core.Payments;

/// <summary>Direct-account, test-key, USD one-time Checkout adapter. No live key, Connect, discounts or tax.</summary>
public sealed class StripeTestCheckoutProvider:ITestCheckoutProvider,IDisposable
{
    private readonly HttpClient http;private readonly string key,account;
    public string Scope=>"stripe-test:"+account;public string ReturnOrigin{get;}
    public StripeTestCheckoutProvider(string key,string account,string returnOrigin,HttpMessageHandler? handler=null)
    {
        if(!key.StartsWith("sk_test_",StringComparison.Ordinal)||key.Length<20||key.Length>256||!PaymentContract.ProviderId(account,"acct_")||!PaymentContract.ReturnOrigin(returnOrigin))throw new ArgumentException("Only configured Stripe test keys and a fixed HTTPS return origin are supported.");
        this.key=key;this.account=account;ReturnOrigin=returnOrigin;http=new(handler??new HttpClientHandler{AllowAutoRedirect=false}){Timeout=Timeout.InfiniteTimeSpan};
    }
    public async Task<HostedTestSession> CreateAsync(CheckoutReservation r,CancellationToken ct)
    {
        if(r.Scope!=Scope||r.ReturnOrigin!=ReturnOrigin||r.Currency!="USD"||r.TotalMinor is <=0 or >1000000)throw new PaymentConflictException();
        using var deadline=CancellationTokenSource.CreateLinkedTokenSource(ct);deadline.CancelAfter(TimeSpan.FromSeconds(20));ct=deadline.Token;
        using(var accountRequest=Request(HttpMethod.Get,"account"))
        {using var data=await Send(accountRequest,ct);if(data.RootElement.GetProperty("id").GetString()!=account)throw new PaymentConflictException();}
        using var request=Request(HttpMethod.Post,"checkout/sessions");request.Headers.Add("Idempotency-Key","gnazzo-test-"+r.AttemptId);
        request.Content=new FormUrlEncodedContent(new Dictionary<string,string>
        {
            ["mode"]="payment",["payment_method_types[0]"]="card",["line_items[0][quantity]"]="1",
            ["line_items[0][price_data][currency]"]="usd",["line_items[0][price_data][unit_amount]"]=r.TotalMinor.ToString(System.Globalization.CultureInfo.InvariantCulture),
            ["line_items[0][price_data][product_data][name]"]="Synthetic song request — TEST ONLY",
            ["client_reference_id"]=r.AttemptId,["metadata[order_id]"]=r.OrderId,
            ["success_url"]=r.ReturnOrigin+"payment-test/return",["cancel_url"]=r.ReturnOrigin+"payment-test/cancel"
        });
        using var doc=await Send(request,ct);var s=doc.RootElement;
        if(s.GetProperty("object").GetString()!="checkout.session"||s.GetProperty("livemode").GetBoolean()||s.GetProperty("mode").GetString()!="payment"||s.GetProperty("client_reference_id").GetString()!=r.AttemptId||s.GetProperty("metadata").GetProperty("order_id").GetString()!=r.OrderId||s.GetProperty("currency").GetString()!="usd"||s.GetProperty("amount_total").GetInt64()!=r.TotalMinor)throw new InvalidDataException("Checkout session differs from server reservation.");
        var result=new HostedTestSession(s.GetProperty("id").GetString()!,s.GetProperty("url").GetString()!);
        if(!PaymentContract.ProviderId(result.Id,"cs_test_")||!PaymentContract.CheckoutUrl(result.Url))throw new InvalidDataException("Invalid test checkout destination.");return result;
    }
    private HttpRequestMessage Request(HttpMethod method,string path)
    {var r=new HttpRequestMessage(method,"https://api.stripe.com/v1/"+path);r.Headers.Authorization=new AuthenticationHeaderValue("Bearer",key);r.Headers.Add("Stripe-Version","2026-10-28.endive");return r;}
    private async Task<JsonDocument> Send(HttpRequestMessage request,CancellationToken ct)
    {
        using var response=await http.SendAsync(request,HttpCompletionOption.ResponseHeadersRead,ct);
        if(!response.IsSuccessStatusCode||response.Content.Headers.ContentType?.MediaType!="application/json")throw new HttpRequestException("Test checkout provider did not confirm the operation.");
        await using var body=await response.Content.ReadAsStreamAsync(ct);using var output=new MemoryStream();var buffer=new byte[4096];int read;
        while((read=await body.ReadAsync(buffer,ct))>0){if(output.Length+read>65536)throw new InvalidDataException("Provider response exceeds policy.");output.Write(buffer,0,read);}
        var doc=JsonDocument.Parse(output.ToArray(),new JsonDocumentOptions{MaxDepth=16});try{PaymentContract.Unique(doc.RootElement);return doc;}catch{doc.Dispose();throw;}
    }
    public void Dispose()=>http.Dispose();
}

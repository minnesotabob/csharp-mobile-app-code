using System.Net;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using GnazzosWorld.Core.Identity;
using GnazzosWorld.Core.Orders;

namespace GnazzosWorld.Core.Commerce;

public sealed class OrderApiException(HttpStatusCode status,OrderProblem? problem):Exception("The server did not confirm this order operation.")
{ public HttpStatusCode Status { get; }=status; public OrderProblem? Problem { get; }=problem; }


/// <summary>Fixed authenticated origin and explicit lease. Injected handlers are trusted; default never follows redirects.</summary>
public sealed class OrderApiClient(ApiSession session,HttpMessageHandler? handler=null):IDisposable
{
    private readonly HttpClient http=new(handler??new HttpClientHandler{AllowAutoRedirect=false}){Timeout=TimeSpan.FromSeconds(20)};
    public async Task<OrderQuote> QuoteAsync(SongRequest draft,OwnedAccessLease owner,CancellationToken ct=default)
    {
        var result=await Send<OrderQuote>(HttpMethod.Post,"api/v1/commerce/quotes",new QuoteCommand(draft),owner,ct);
        Validate(result); if(result.RequestHash!=OrderContract.Fingerprint(draft))throw new InvalidDataException("Quote changed the request.");return result;
    }
    public async Task<OrderSnapshot> AcceptAsync(OrderQuote quote,string key,OwnedAccessLease owner,CancellationToken ct=default)
    {
        Validate(quote);if(!OrderContract.Id(key))throw new InvalidDataException("Invalid operation key.");
        var result=await Send<OrderSnapshot>(HttpMethod.Post,"api/v1/commerce/orders",new AcceptOrderCommand(quote.Id,key,quote.Request),owner,ct);
        Validate(result,quote,key);return result;
    }
    public async Task<OrderSnapshot> GetAsync(string id,OwnedAccessLease owner,CancellationToken ct=default)
    {
        if(!OrderContract.Id(id))throw new InvalidDataException("Invalid order ID.");
        var result=await Send<OrderSnapshot>(HttpMethod.Get,"api/v1/commerce/orders/"+id,null,owner,ct);
        Validate(new OrderQuote(result.QuoteId,result.Request,result.RequestHash,result.PricingVersion,result.Currency,result.TotalMinor,DateTimeOffset.MinValue));
        if(result.Id!=id || !OrderContract.Id(result.QuoteId)||!OrderContract.Id(result.OperationKey)||result.Version<=0||!Enum.IsDefined(result.State)||result.RequestHash!=OrderContract.Fingerprint(result.Request))throw new InvalidDataException("Invalid owned order.");return result;
    }
    public async Task<GnazzosWorld.Core.Payments.TestPaymentReceipt> GetTestPaymentAsync(string id,OwnedAccessLease owner,CancellationToken ct=default)
    {
        if(!OrderContract.Id(id))throw new InvalidDataException("Invalid order ID.");
        var result=await Send<GnazzosWorld.Core.Payments.TestPaymentReceipt>(HttpMethod.Get,"api/v1/commerce/orders/"+id+"/test-payment",null,owner,ct);
        GnazzosWorld.Core.Payments.PaymentContract.Validate(result,id);return result;
    }
    private async Task<T> Send<T>(HttpMethod method,string path,object? body,OwnedAccessLease owner,CancellationToken ct) where T:class
    {
        if(!session.IsCurrent(owner.Access))throw new SessionEndedException();
        using var request=new HttpRequestMessage(method,new Uri(session.Settings.ApiOrigin,path));
        request.Headers.Authorization=new AuthenticationHeaderValue("Bearer",owner.Access.Token);
        if(body is not null)
        {
            var text=JsonSerializer.Serialize(body,body.GetType(),OrderContract.Json);
            if(Encoding.UTF8.GetByteCount(text)>16384)throw new InvalidDataException("Order body exceeds policy.");
            request.Content=new StringContent(text,Encoding.UTF8,"application/json");
        }
        using var response=await http.SendAsync(request,HttpCompletionOption.ResponseHeadersRead,ct);
        ct.ThrowIfCancellationRequested();if(!session.IsCurrent(owner.Access))throw new SessionEndedException();
        if(response.StatusCode==HttpStatusCode.Unauthorized){await session.RejectLeaseAsync(owner.Access,ct);throw new InteractiveSignInRequiredException();}
        if(!response.IsSuccessStatusCode)
        {
            OrderProblem? problem=null;
            if(response.Content.Headers.ContentType?.MediaType=="application/problem+json")
            {
                await using var error=await response.Content.ReadAsStreamAsync(ct);using var bounded=new MemoryStream();var small=new byte[1024];int read;
                while((read=await error.ReadAsync(small,ct))!=0){if(bounded.Length+read>8192)throw new InvalidDataException("Order error response exceeds policy.");bounded.Write(small,0,read);}
                try{using var doc=JsonDocument.Parse(bounded.ToArray());if(doc.RootElement.TryGetProperty("code",out var code)&&code.ValueKind==JsonValueKind.String&&Enum.TryParse<OrderProblem>(code.GetString(),out var parsed)&&Enum.IsDefined(parsed))problem=parsed;}
                catch(JsonException){ }
            }
            ct.ThrowIfCancellationRequested();if(!session.IsCurrent(owner.Access))throw new SessionEndedException();
            throw new OrderApiException(response.StatusCode,problem);
        }
        if(response.Content.Headers.ContentType?.MediaType!="application/json")throw new InvalidDataException("Expected order JSON.");
        await using var input=await response.Content.ReadAsStreamAsync(ct);using var output=new MemoryStream();var buffer=new byte[4096];int count;
        while((count=await input.ReadAsync(buffer,ct))!=0){if(output.Length+count>32768)throw new InvalidDataException("Order response exceeds policy.");output.Write(buffer,0,count);}
        var result=JsonSerializer.Deserialize<T>(output.ToArray(),OrderContract.Json)??throw new InvalidDataException("Invalid order response.");
        ct.ThrowIfCancellationRequested();if(!session.IsCurrent(owner.Access))throw new SessionEndedException();return result;
    }
    public static void Validate(OrderQuote q)
    {
        if(!OrderContract.Id(q.Id)||q.RequestHash!=OrderContract.Fingerprint(q.Request)||q.Currency!="USD"||q.TotalMinor is <=0 or >1000000||
            string.IsNullOrWhiteSpace(q.PricingVersion)||q.PricingVersion.Length>64||q.PricingVersion.Any(char.IsControl))throw new InvalidDataException("Invalid quote snapshot.");
    }
    public static void Validate(OrderSnapshot o,OrderQuote q,string key)
    {
        if(!OrderContract.Id(o.Id)||o.QuoteId!=q.Id||o.OperationKey!=key||o.RequestHash!=q.RequestHash||OrderContract.Fingerprint(o.Request)!=q.RequestHash||
            o.PricingVersion!=q.PricingVersion||o.Currency!=q.Currency||o.TotalMinor!=q.TotalMinor||o.Version<=0||!Enum.IsDefined(o.State))throw new InvalidDataException("Order receipt differs from the persisted acceptance.");
    }
    public void Dispose()=>http.Dispose();
}

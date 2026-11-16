using System.Net;
using System.Net.Http.Headers;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using GnazzosWorld.Core.Commerce;
using GnazzosWorld.Core.Identity;

namespace GnazzosWorld.Core.Fulfillment;

public sealed class TrackingApiException(HttpStatusCode status,OrderProblem? problem):Exception("The owned tracking operation was not confirmed.")
{public HttpStatusCode Status{get;}=status;public OrderProblem? Problem{get;}=problem;}
public sealed class VerifiedDelivery:IDisposable
{
    private readonly byte[] bytes;internal VerifiedDelivery(byte[] bytes){this.bytes=bytes;}
    public ReadOnlyMemory<byte> Bytes=>bytes;
    public override string ToString()=>"Verified synthetic delivery [bytes omitted]";
    public void Dispose()=>CryptographicOperations.ZeroMemory(bytes);
}
public sealed class TrackingApiClient(ApiSession session,HttpMessageHandler? handler=null,TimeProvider? clock=null):IDisposable
{
    private readonly HttpClient http=new(handler??new HttpClientHandler{AllowAutoRedirect=false}){Timeout=Timeout.InfiniteTimeSpan};
    private readonly TimeProvider time=clock??TimeProvider.System;
    public CommerceAccount Account(OwnedAccessLease lease)
    {Current(lease,CancellationToken.None);return new(new(lease.Issuer.AbsoluteUri,lease.Subject),session.Settings.ApiOrigin.AbsoluteUri,session.Settings.Audience);}
    private void Current(OwnedAccessLease lease,CancellationToken ct){ct.ThrowIfCancellationRequested();if(!session.IsCurrent(lease.Access))throw new SessionEndedException();}
    private static string OrderPath(string id){if(!OrderContract.Id(id))throw new InvalidDataException("Invalid order ID.");return "api/v1/commerce/orders/"+id;}
    public async Task<OrderPage> ListAsync(string? after,OwnedAccessLease lease,CancellationToken ct=default)
    {
        if(after is not null&&!OrderContract.Id(after))throw new InvalidDataException("Invalid order cursor.");
        var result=await Json<OrderPage>(HttpMethod.Get,"api/v1/commerce/orders"+(after is null?"":"?after="+after),null,lease,ct);
        if(result.Items is null||result.Items.Count>20||result.Items.Any(x=>!OrderContract.Id(x.Id)||!Enum.IsDefined(x.State)||x.Version<1)||result.Items.Select(x=>x.Id).Distinct().Count()!=result.Items.Count||
           !result.Items.Select(x=>x.Id).SequenceEqual(result.Items.Select(x=>x.Id).Order(StringComparer.Ordinal))||result.Items.Any(x=>after is not null&&string.CompareOrdinal(x.Id,after)<=0)||
           (result.Next is not null&&(result.Items.Count!=20||result.Next!=result.Items[^1].Id)))throw new InvalidDataException("Invalid owned order page.");return result;
    }
    public async Task<OrderTracking> TrackAsync(string id,OwnedAccessLease lease,CancellationToken ct=default)
    {
        var t=await Json<OrderTracking>(HttpMethod.Get,OrderPath(id)+"/tracking",null,lease,ct);
        if(t.Order is null||t.Order.Id!=id||!Enum.IsDefined(t.Order.State)||t.Order.Version is <=0 or >100||t.Order.RequestHash!=OrderContract.Fingerprint(t.Order.Request)||t.Order.TotalMinor is <=0 or >1000000||t.Order.Currency!="USD"||
           t.History is null||t.History.Count!=t.Order.Version||t.History.Where((h,i)=>h.Version!=i+1||!Enum.IsDefined(h.State)).Any()||t.History[^1].State!=t.Order.State||
           t.Revisions is null||t.Revisions.Count>10||t.Deliveries is null||t.Deliveries.Count>1||t.Notices is null||t.Notices.Count>100)throw new InvalidDataException("Invalid tracking snapshot.");
        foreach(var r in t.Revisions){TrackingContract.Revision(r,id,new(r.BaseVersion,r.OperationKey,r.Note));if(!OrderContract.Id(r.OperationKey)||r.AppliedVersion>t.Order.Version)throw new InvalidDataException("Invalid revision history.");}
        foreach(var d in t.Deliveries){TrackingContract.Descriptor(d,id);if(d.Version>t.Order.Version)throw new InvalidDataException("Invalid delivery version.");}
        if(t.Notices.Any(n=>!OrderContract.Id(n.Id)||n.Version<1||n.Version>t.Order.Version||!Enum.IsDefined(n.Kind)))throw new InvalidDataException("Invalid order notices.");return t;
    }
    public async Task<RevisionReceipt> ReviseAsync(string id,RevisionCommand command,OwnedAccessLease lease,CancellationToken ct=default)
    {if(command.ExpectedVersion<=0||!OrderContract.Id(command.OperationKey))throw new InvalidDataException("Invalid revision operation.");_=TrackingContract.Note(command.Note);var receipt=await Json<RevisionReceipt>(HttpMethod.Post,OrderPath(id)+"/revisions",command,lease,ct);TrackingContract.Revision(receipt,id,command);return receipt;}
    public async Task<VerifiedDelivery> FetchAsync(string orderId,DeliveryDescriptor delivery,long expectedVersion,OwnedAccessLease lease,CancellationToken ct=default)
    {
        TrackingContract.Descriptor(delivery,orderId);if(delivery.Revoked||delivery.Version!=expectedVersion)throw new InvalidDataException("Refresh delivery before access.");
        using var deadline=CancellationTokenSource.CreateLinkedTokenSource(ct);deadline.CancelAfter(TimeSpan.FromSeconds(20));ct=deadline.Token;
        var path=OrderPath(orderId)+"/deliveries/"+delivery.Id;
        var grant=await Json<DeliveryGrant>(HttpMethod.Post,path+"/grant",new{ExpectedVersion=expectedVersion},lease,ct);
        if(grant.Delivery!=delivery||!TrackingContract.GrantToken(grant.Token)||grant.ExpiresAt<=time.GetUtcNow()||grant.ExpiresAt>time.GetUtcNow().AddMinutes(3))throw new InvalidDataException("Invalid delivery grant.");
        var bytes=await Send(HttpMethod.Get,path+"/content",null,lease,ct,"audio/wav",checked((int)delivery.Length),grant.Token);
        if(bytes.LongLength!=delivery.Length||Convert.ToHexString(SHA256.HashData(bytes)).ToLowerInvariant()!=delivery.Sha256){CryptographicOperations.ZeroMemory(bytes);throw new InvalidDataException("Delivery integrity failed.");}
        try{Current(lease,ct);return new(bytes);}catch{CryptographicOperations.ZeroMemory(bytes);throw;}
    }
    private async Task<T> Json<T>(HttpMethod method,string path,object? body,OwnedAccessLease lease,CancellationToken ct) where T:class
    {var bytes=await Send(method,path,body,lease,ct,"application/json",262144);return JsonSerializer.Deserialize<T>(bytes,OrderContract.Json)??throw new InvalidDataException("Invalid tracking JSON.");}
    private async Task<byte[]> Send(HttpMethod method,string path,object? body,OwnedAccessLease lease,CancellationToken ct,string mediaType,int cap,string? grant=null)
    {
        using var deadline=CancellationTokenSource.CreateLinkedTokenSource(ct);deadline.CancelAfter(TimeSpan.FromSeconds(20));ct=deadline.Token;Current(lease,ct);
        using var request=new HttpRequestMessage(method,new Uri(session.Settings.ApiOrigin,path));request.Headers.Authorization=new AuthenticationHeaderValue("Bearer",lease.Access.Token);
        if(grant is not null)request.Headers.Add("X-Delivery-Grant",grant);
        if(body is not null){var text=JsonSerializer.Serialize(body,body.GetType(),OrderContract.Json);if(Encoding.UTF8.GetByteCount(text)>16384)throw new InvalidDataException("Revision body exceeds policy.");request.Content=new StringContent(text,Encoding.UTF8,"application/json");}
        using var response=await http.SendAsync(request,HttpCompletionOption.ResponseHeadersRead,ct);Current(lease,ct);
        if(response.StatusCode==HttpStatusCode.Unauthorized){await session.RejectLeaseAsync(lease.Access,ct);throw new InteractiveSignInRequiredException();}
        var bytes=await Read(response,response.IsSuccessStatusCode?cap:8192,ct);Current(lease,ct);
        if(!response.IsSuccessStatusCode)
        {
            OrderProblem? problem=null;
            try{using var d=JsonDocument.Parse(bytes);if(d.RootElement.TryGetProperty("code",out var code)&&code.ValueKind==JsonValueKind.String&&Enum.TryParse<OrderProblem>(code.GetString(),out var parsed)&&Enum.IsDefined(parsed))problem=parsed;}catch(JsonException){ }
            throw new TrackingApiException(response.StatusCode,problem);
        }
        if(response.StatusCode!=HttpStatusCode.OK||response.Content.Headers.ContentType?.MediaType!=mediaType)throw new InvalidDataException("Unexpected delivery representation.");return bytes;
    }
    private static async Task<byte[]> Read(HttpResponseMessage response,int cap,CancellationToken ct)
    {await using var input=await response.Content.ReadAsStreamAsync(ct);using var output=new MemoryStream();var buffer=new byte[4096];int read;while((read=await input.ReadAsync(buffer,ct))>0){if(output.Length+read>cap)throw new InvalidDataException("Tracking response exceeds policy.");output.Write(buffer,0,read);}return output.ToArray();}
    public void Dispose()=>http.Dispose();
}

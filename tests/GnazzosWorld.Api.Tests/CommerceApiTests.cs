using System.Net;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using GnazzosWorld.Core.Commerce;
using GnazzosWorld.Core.Identity;
using GnazzosWorld.Core.Orders;
using Xunit;

namespace GnazzosWorld.Api.Tests;

public sealed class CommerceApiTests:IDisposable
{
    private static CancellationToken Ct=>TestContext.Current.CancellationToken;
    private readonly string root=Path.Combine(Path.GetTempPath(),"gnazzo-commerce-api-"+Guid.NewGuid().ToString("N"));
    private string Database=>Path.Combine(root,"server.db");
    private readonly Clock clock=new();
    private static SongRequest Draft()=>new(){Genre="Folk",Occasion="Synthetic anniversary",Story="Invented API request",ContactName="Sample Reader",Email="reader@example.test"};
    private static string Key()=>Guid.NewGuid().ToString("N");
    private static async Task<OrderQuote> Quote(HttpClient client)
    {
        using var response=await client.PostAsJsonAsync("/api/v1/commerce/quotes",new QuoteCommand(Draft()),OrderContract.Json,Ct);
        Assert.Equal(HttpStatusCode.OK,response.StatusCode);return (await response.Content.ReadFromJsonAsync<OrderQuote>(OrderContract.Json,Ct))!;
    }
    private static async Task<OrderSnapshot> Accept(HttpClient c,OrderQuote q,string key)
    {
        using var response=await c.PostAsJsonAsync("/api/v1/commerce/orders",new AcceptOrderCommand(q.Id,key,q.Request),OrderContract.Json,Ct);
        Assert.Equal(HttpStatusCode.OK,response.StatusCode);return (await response.Content.ReadFromJsonAsync<OrderSnapshot>(OrderContract.Json,Ct))!;
    }
    [Fact]
    public async Task Commerce_routes_are_absent_by_default()
    {
        using var f=new RequestAuthorizationTests.Factory();using var c=f.Client(f.Token(scope:"orders:read orders:write"));
        using var response=await c.PostAsJsonAsync("/api/v1/commerce/quotes",new QuoteCommand(Draft()),OrderContract.Json,Ct);Assert.Equal(HttpStatusCode.NotFound,response.StatusCode);
    }
    [Fact]
    public async Task Credential_and_dedicated_write_scope_are_required()
    {
        using var store=new SqliteOrderStore(Database,clock);using var f=new RequestAuthorizationTests.Factory(commerceStore:store);
        using var anonymous=f.Client();using var noAuth=await anonymous.PostAsJsonAsync("/api/v1/commerce/quotes",new QuoteCommand(Draft()),OrderContract.Json,Ct);Assert.Equal(HttpStatusCode.Unauthorized,noAuth.StatusCode);
        using var preview=f.Client(f.Token());using var denied=await preview.PostAsJsonAsync("/api/v1/commerce/quotes",new QuoteCommand(Draft()),OrderContract.Json,Ct);Assert.Equal(HttpStatusCode.Forbidden,denied.StatusCode);
    }
    [Fact]
    public async Task Owned_quote_acceptance_and_read_use_no_store_and_server_snapshot()
    {
        using var store=new SqliteOrderStore(Database,clock);using var f=new RequestAuthorizationTests.Factory(commerceStore:store);using var c=f.Client(f.Token(scope:"orders:read orders:write"));
        var q=await Quote(c);Assert.Equal(17900,q.TotalMinor);var order=await Accept(c,q,Key());
        using var response=await c.GetAsync("/api/v1/commerce/orders/"+order.Id,Ct);Assert.Equal(HttpStatusCode.OK,response.StatusCode);Assert.Contains("no-store",response.Headers.CacheControl!.ToString());
        var owned=await response.Content.ReadFromJsonAsync<OrderSnapshot>(OrderContract.Json,Ct);Assert.Equal(order,owned);Assert.Equal(OrderState.Accepted,owned!.State);Assert.Single(store.HistoryForLab(order.Id));
    }
    [Fact]
    public async Task Duplicate_POST_after_expiry_returns_same_owned_order()
    {
        using var store=new SqliteOrderStore(Database,clock);using var f=new RequestAuthorizationTests.Factory(commerceStore:store);using var c=f.Client(f.Token(scope:"orders:write"));
        var q=await Quote(c);var key=Key();var first=await Accept(c,q,key);clock.Now=clock.Now.AddDays(1);var replay=await Accept(c,q,key);Assert.Equal(first.Id,replay.Id);Assert.Single(store.HistoryForLab(first.Id));
    }
    [Fact]
    public async Task Expired_quote_that_was_never_accepted_is_a_conflict()
    {
        using var store=new SqliteOrderStore(Database,clock);using var f=new RequestAuthorizationTests.Factory(commerceStore:store);using var c=f.Client(f.Token(scope:"orders:write"));
        var q=await Quote(c);clock.Now=q.ExpiresAt;using var r=await c.PostAsJsonAsync("/api/v1/commerce/orders",new AcceptOrderCommand(q.Id,Key(),q.Request),OrderContract.Json,Ct);
        Assert.Equal(HttpStatusCode.Conflict,r.StatusCode);using var json=JsonDocument.Parse(await r.Content.ReadAsStringAsync(Ct));Assert.Equal("ExpiredQuote",json.RootElement.GetProperty("code").GetString());
    }
    [Fact]
    public async Task Different_account_cannot_accept_quote_or_read_order()
    {
        using var store=new SqliteOrderStore(Database,clock);using var f=new RequestAuthorizationTests.Factory(commerceStore:store);
        using var a=f.Client(f.Token(scope:"orders:read orders:write"));using var b=f.Client(f.Token(subject:"reader-b",scope:"orders:read orders:write"));
        var q=await Quote(a);using var foreign=await b.PostAsJsonAsync("/api/v1/commerce/orders",new AcceptOrderCommand(q.Id,Key(),q.Request),OrderContract.Json,Ct);Assert.Equal(HttpStatusCode.NotFound,foreign.StatusCode);
        var order=await Accept(a,q,Key());using var read=await b.GetAsync("/api/v1/commerce/orders/"+order.Id,Ct);Assert.Equal(HttpStatusCode.NotFound,read.StatusCode);Assert.DoesNotContain(Draft().Story,await read.Content.ReadAsStringAsync(Ct));
    }
    [Fact]
    public async Task Read_permission_does_not_allow_write_and_write_permission_does_not_allow_read()
    {
        using var store=new SqliteOrderStore(Database,clock);using var f=new RequestAuthorizationTests.Factory(commerceStore:store);using var write=f.Client(f.Token(scope:"orders:write"));
        var order=await Accept(write,await Quote(write),Key());using var denied=await write.GetAsync("/api/v1/commerce/orders/"+order.Id,Ct);Assert.Equal(HttpStatusCode.Forbidden,denied.StatusCode);
        using var read=f.Client(f.Token(scope:"orders:read"));using var noWrite=await read.PostAsJsonAsync("/api/v1/commerce/quotes",new QuoteCommand(Draft()),OrderContract.Json,Ct);Assert.Equal(HttpStatusCode.Forbidden,noWrite.StatusCode);
    }
    [Theory]
    [InlineData("owner")][InlineData("totalMinor")][InlineData("paid")][InlineData("state")]
    public async Task Client_cannot_post_owner_price_payment_or_state_fields(string field)
    {
        using var store=new SqliteOrderStore(Database,clock);using var f=new RequestAuthorizationTests.Factory(commerceStore:store);using var c=f.Client(f.Token(scope:"orders:write"));
        var request=JsonSerializer.Serialize(new QuoteCommand(Draft()),OrderContract.Json);using var body=new StringContent(request[..^1]+",\""+field+"\":\"forged\"}",Encoding.UTF8,"application/json");
        using var response=await c.PostAsync("/api/v1/commerce/quotes",body,Ct);Assert.Equal(HttpStatusCode.BadRequest,response.StatusCode);
    }
    [Fact]
    public async Task Oversized_duplicate_or_null_payload_is_rejected_without_server_error()
    {
        using var store=new SqliteOrderStore(Database,clock);using var f=new RequestAuthorizationTests.Factory(commerceStore:store);using var c=f.Client(f.Token(scope:"orders:write"));
        foreach(var data in new[]{new string('x',16385),"{\"request\":null}","{\"request\":null,\"Request\":null}"})
        {using var body=new StringContent(data,Encoding.UTF8,"application/json");using var r=await c.PostAsync("/api/v1/commerce/quotes",body,Ct);Assert.Equal(HttpStatusCode.BadRequest,r.StatusCode);}
    }
    [Fact]
    public async Task Reusing_quote_with_another_key_and_changed_payload_is_not_second_acceptance()
    {
        using var store=new SqliteOrderStore(Database,clock);using var f=new RequestAuthorizationTests.Factory(commerceStore:store);using var c=f.Client(f.Token(scope:"orders:write"));
        var q=await Quote(c);var key=Key();await Accept(c,q,key);
        using var used=await c.PostAsJsonAsync("/api/v1/commerce/orders",new AcceptOrderCommand(q.Id,Key(),q.Request),OrderContract.Json,Ct);Assert.Equal(HttpStatusCode.Conflict,used.StatusCode);
        using var changed=await c.PostAsJsonAsync("/api/v1/commerce/orders",new AcceptOrderCommand(q.Id,key,q.Request with{Quantity=2}),OrderContract.Json,Ct);Assert.Equal(HttpStatusCode.Conflict,changed.StatusCode);
    }
    [Fact]
    public async Task Customer_has_no_status_or_payment_update_route()
    {
        using var store=new SqliteOrderStore(Database,clock);using var f=new RequestAuthorizationTests.Factory(commerceStore:store);using var c=f.Client(f.Token(scope:"orders:read orders:write"));
        var order=await Accept(c,await Quote(c),Key());using var response=await c.PutAsJsonAsync("/api/v1/commerce/orders/"+order.Id,new{state="Delivered",paid=true},Ct);Assert.Equal(HttpStatusCode.MethodNotAllowed,response.StatusCode);Assert.Equal(OrderState.Accepted,store.Get(new("https://fixture.auth0.com/","reader-a"),order.Id)!.State);
    }
    [Fact]
    public async Task Actual_native_client_flow_persists_acceptance_before_signed_HTTP_and_refreshes_receipt()
    {
        using var store=new SqliteOrderStore(Database,clock);using var f=new RequestAuthorizationTests.Factory(commerceStore:store);using var bootstrap=f.Client();
        var settings=new ApiSessionSettings(new("fixture.auth0.com","public-client-id"),"https://gnazzosworld.example/api",new("https://localhost"));
        var session=new ApiSession(settings,new Vault(new(settings.Identity.Issuer,"reader-a",settings.Audience,f.Token(scope:"orders:read orders:write"),null,DateTimeOffset.UtcNow.AddMinutes(4))),new Provider(),TimeProvider.System);
        using var api=new OrderApiClient(session,f.Server.CreateHandler());using var pending=new PendingOrderDatabase(Path.Combine(root,"client.db"));var flow=new OrderAttemptCoordinator(session,api,pending);var lease=await session.AcquireOwnedAsync(Ct);
        var q=await api.QuoteAsync(Draft(),lease,Ct);var order=await flow.AcceptAsync(q,lease,Ct);Assert.Equal(order,pending.Read(flow.Account(lease))!.Confirmed);
        var next=store.AdvanceForLab(order.Id,1,OrderState.InProduction);Assert.Equal(next,await flow.ResumeAsync(lease,Ct));
    }
    private sealed class Clock:TimeProvider
    {public DateTimeOffset Now=new(2026,11,15,12,0,0,TimeSpan.Zero);public override DateTimeOffset GetUtcNow()=>Now;}
    private sealed class Vault(SessionGrant grant):ISessionVault
    {private SessionGrant? value=grant;public Task<SessionGrant?> ReadAsync(CancellationToken ct)=>Task.FromResult(value);public Task WriteAsync(SessionGrant? v,CancellationToken ct){value=v;return Task.CompletedTask;}}
    private sealed class Provider:ISessionProvider
    {public Task<SessionGrant> SignInAsync(CancellationToken ct)=>throw new InvalidOperationException();public Task<SessionGrant> RefreshAsync(SessionGrant previous,CancellationToken ct)=>throw new InvalidOperationException();}
    public void Dispose(){if(Directory.Exists(root))Directory.Delete(root,recursive:true);}
}

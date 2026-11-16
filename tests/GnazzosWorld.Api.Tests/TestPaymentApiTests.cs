using System.Net;
using System.Net.Http.Json;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using GnazzosWorld.Core.Commerce;
using GnazzosWorld.Core.Orders;
using GnazzosWorld.Core.Payments;
using Xunit;

namespace GnazzosWorld.Api.Tests;

public sealed class TestPaymentApiTests:IDisposable
{
    private static CancellationToken Ct=>TestContext.Current.CancellationToken;
    private readonly string root=Path.Combine(Path.GetTempPath(),"gnazzo-payment-api-"+Guid.NewGuid().ToString("N"));
    private const string Secret="whsec_synthetic_fixture_secret_0123456789";
    private static readonly OrderOwner A=new("https://fixture.auth0.com/","reader-a");
    private SqliteOrderStore Store()=>new(Path.Combine(root,Guid.NewGuid().ToString("N")+".db"),TimeProvider.System);
    private static OrderSnapshot Order(SqliteOrderStore s){var q=s.CreateQuote(A,new SongRequest{Genre="Folk",Occasion="Invented",Story="Synthetic private story",ContactName="Sample Reader",Email="reader@example.test"});return s.Accept(A,new(q.Id,Guid.NewGuid().ToString("N"),q.Request));}
    private static string Checkout(OrderSnapshot o)=>"/api/v1/commerce/orders/"+o.Id+"/test-checkout";
    private static string Payment(OrderSnapshot o)=>"/api/v1/commerce/orders/"+o.Id+"/test-payment";
    private static async Task<TestPaymentReceipt> Start(HttpClient c,OrderSnapshot o){using var r=await c.PostAsync(Checkout(o),null,Ct);Assert.Equal(HttpStatusCode.OK,r.StatusCode);return (await r.Content.ReadFromJsonAsync<TestPaymentReceipt>(OrderContract.Json,Ct))!;}
    private static byte[] Event(OrderSnapshot o,TestPaymentReceipt p,string id="evt_api",string type="checkout.session.completed",string status="paid")=>JsonSerializer.SerializeToUtf8Bytes(new{id,@object="event",livemode=false,type,data=new{@object=new{id=p.SessionId,@object="checkout.session",livemode=false,mode="payment",client_reference_id=p.AttemptId,metadata=new{order_id=o.Id},currency="usd",amount_total=o.TotalMinor,payment_status=status,payment_intent=status=="paid"?"pi_api":null}}});
    private static async Task<HttpResponseMessage> Deliver(HttpClient c,byte[] bytes,bool signed=true)
    {using var request=new HttpRequestMessage(HttpMethod.Post,"/api/v1/commerce/test-payments/webhook"){Content=new ByteArrayContent(bytes)};request.Content.Headers.ContentType=new("application/json");if(signed){var t=DateTimeOffset.UtcNow.ToUnixTimeSeconds();var material=Encoding.ASCII.GetBytes(t+".").Concat(bytes).ToArray();request.Headers.Add("Stripe-Signature","t="+t+",v1="+Convert.ToHexString(HMACSHA256.HashData(Encoding.UTF8.GetBytes(Secret),material)));}return await c.SendAsync(request,Ct);}
    [Fact] public async Task Payment_routes_are_disabled_by_default()
    {using var f=new RequestAuthorizationTests.Factory();using var c=f.Client(f.Token(scope:"orders:write"));using var r=await c.PostAsync("/api/v1/commerce/orders/"+new string('a',32)+"/test-checkout",null,Ct);Assert.Equal(HttpStatusCode.NotFound,r.StatusCode);}
    [Fact] public async Task Checkout_requires_identity_scope_and_owned_order()
    {using var s=Store();var o=Order(s);using var f=new RequestAuthorizationTests.Factory(commerceStore:s,paymentProvider:new Provider());using var anonymous=f.Client();using var unauth=await anonymous.PostAsync(Checkout(o),null,Ct);Assert.Equal(HttpStatusCode.Unauthorized,unauth.StatusCode);using var read=f.Client(f.Token(scope:"orders:read"));using var forbidden=await read.PostAsync(Checkout(o),null,Ct);Assert.Equal(HttpStatusCode.Forbidden,forbidden.StatusCode);using var b=f.Client(f.Token(subject:"reader-b",scope:"orders:write"));using var other=await b.PostAsync(Checkout(o),null,Ct);Assert.Equal(HttpStatusCode.NotFound,other.StatusCode);}
    [Fact] public async Task Signed_sandbox_payment_reconciles_server_side_and_duplicate_delivery_is_safe()
    {using var s=Store();var o=Order(s);var provider=new Provider();using var f=new RequestAuthorizationTests.Factory(commerceStore:s,paymentProvider:provider);using var c=f.Client(f.Token(scope:"orders:read orders:write"));var pending=await Start(c,o);Assert.Equal(pending,await Start(c,o));Assert.Equal(1,provider.Calls);using var webhook=f.Client();var bytes=Event(o,pending);using var first=await Deliver(webhook,bytes);using var duplicate=await Deliver(webhook,bytes);Assert.Equal(HttpStatusCode.OK,first.StatusCode);Assert.Equal(HttpStatusCode.OK,duplicate.StatusCode);using var read=await c.GetAsync(Payment(o),Ct);Assert.Contains("no-store",read.Headers.CacheControl!.ToString());var paid=await read.Content.ReadFromJsonAsync<TestPaymentReceipt>(OrderContract.Json,Ct);Assert.Equal(TestPaymentState.Paid,paid!.State);Assert.Equal(3,paid.Version);Assert.Null(paid.CheckoutUrl);Assert.Equal(OrderState.Accepted,s.Get(A,o.Id)!.State);}
    [Theory][InlineData("/payment-test/return?paid=true&session_id=forged")][InlineData("/payment-test/cancel?paid=true")]
    public async Task Browser_return_and_cancel_query_cannot_mark_paid(string path)
    {using var s=Store();var o=Order(s);using var f=new RequestAuthorizationTests.Factory(commerceStore:s,paymentProvider:new Provider());using var c=f.Client(f.Token(scope:"orders:read orders:write"));await Start(c,o);using var response=await c.GetAsync(path,Ct);Assert.Equal(HttpStatusCode.OK,response.StatusCode);Assert.Contains("does not establish payment",await response.Content.ReadAsStringAsync(Ct));Assert.Equal(TestPaymentState.Pending,s.GetTestPayment(A,o.Id)!.State);}
    [Fact] public async Task Forged_or_oversized_webhook_cannot_mark_paid()
    {using var s=Store();var o=Order(s);using var f=new RequestAuthorizationTests.Factory(commerceStore:s,paymentProvider:new Provider());using var c=f.Client(f.Token(scope:"orders:read orders:write"));var pending=await Start(c,o);using var anonymous=f.Client();using var bad=await Deliver(anonymous,Event(o,pending),false);Assert.Equal(HttpStatusCode.BadRequest,bad.StatusCode);using var huge=await Deliver(anonymous,new byte[65537]);Assert.Equal(HttpStatusCode.BadRequest,huge.StatusCode);Assert.Equal(TestPaymentState.Pending,s.GetTestPayment(A,o.Id)!.State);}
    [Fact] public async Task Client_cannot_post_card_price_paid_or_return_URL_body()
    {using var s=Store();var o=Order(s);var provider=new Provider();using var f=new RequestAuthorizationTests.Factory(commerceStore:s,paymentProvider:provider);using var c=f.Client(f.Token(scope:"orders:write"));using var r=await c.PostAsJsonAsync(Checkout(o),new{paid=true,amount=1,returnUrl="https://evil.example",card="not-a-real-card"},Ct);Assert.Equal(HttpStatusCode.BadRequest,r.StatusCode);Assert.Equal(0,provider.Calls);Assert.Null(s.GetTestPayment(A,o.Id));}
    [Fact] public async Task Private_payment_and_checkout_URL_are_unavailable_to_other_account()
    {using var s=Store();var o=Order(s);using var f=new RequestAuthorizationTests.Factory(commerceStore:s,paymentProvider:new Provider());using var a=f.Client(f.Token(scope:"orders:read orders:write"));var pending=await Start(a,o);using var b=f.Client(f.Token(subject:"reader-b",scope:"orders:read"));using var response=await b.GetAsync(Payment(o),Ct);Assert.Equal(HttpStatusCode.NotFound,response.StatusCode);Assert.DoesNotContain(pending.CheckoutUrl!,await response.Content.ReadAsStringAsync(Ct));}
    [Fact] public async Task Authenticated_unpaid_then_async_success_and_late_failure_converge()
    {using var s=Store();var o=Order(s);using var f=new RequestAuthorizationTests.Factory(commerceStore:s,paymentProvider:new Provider());using var c=f.Client(f.Token(scope:"orders:read orders:write"));var p=await Start(c,o);using var webhook=f.Client();using var unpaid=await Deliver(webhook,Event(o,p,"evt_unpaid",status:"unpaid"));Assert.Equal(HttpStatusCode.OK,unpaid.StatusCode);Assert.Equal(TestPaymentState.Pending,s.GetTestPayment(A,o.Id)!.State);using var success=await Deliver(webhook,Event(o,p,"evt_success","checkout.session.async_payment_succeeded"));using var late=await Deliver(webhook,Event(o,p,"evt_late","checkout.session.async_payment_failed","unpaid"));Assert.Equal(HttpStatusCode.OK,success.StatusCode);Assert.Equal(HttpStatusCode.OK,late.StatusCode);Assert.Equal(TestPaymentState.Paid,s.GetTestPayment(A,o.Id)!.State);}
    [Fact] public async Task Unknown_session_mapping_is_retryable_not_successfully_discarded()
    {using var s=Store();var o=Order(s);using var f=new RequestAuthorizationTests.Factory(commerceStore:s,paymentProvider:new Provider());using var c=f.Client(f.Token(scope:"orders:read orders:write"));var p=await Start(c,o);using var webhook=f.Client();using var r=await Deliver(webhook,Event(o,p with{SessionId="cs_test_unknown"}));Assert.Equal(HttpStatusCode.ServiceUnavailable,r.StatusCode);Assert.Equal(TestPaymentState.Pending,s.GetTestPayment(A,o.Id)!.State);}
    private sealed class Provider:ITestCheckoutProvider
    {public int Calls;public string Scope=>"stripe-test:acct_fixture";public string ReturnOrigin=>"https://localhost/";public Task<HostedTestSession> CreateAsync(CheckoutReservation r,CancellationToken ct){Calls++;return Task.FromResult(new HostedTestSession("cs_test_"+r.AttemptId,"https://checkout.stripe.com/c/pay/"+r.AttemptId));}}
    public void Dispose(){if(Directory.Exists(root))Directory.Delete(root,true);}
}

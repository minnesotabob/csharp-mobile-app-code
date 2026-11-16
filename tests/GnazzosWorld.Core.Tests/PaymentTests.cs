using System.Net;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using GnazzosWorld.Core.Commerce;
using GnazzosWorld.Core.Orders;
using GnazzosWorld.Core.Payments;
using Microsoft.Data.Sqlite;
using Xunit;

namespace GnazzosWorld.Core.Tests;

public sealed class PaymentTests:IDisposable
{
    private static CancellationToken Ct=>TestContext.Current.CancellationToken;
    private readonly string root=Path.Combine(Path.GetTempPath(),"gnazzo-payments-"+Guid.NewGuid().ToString("N"));private string Db=>Path.Combine(root,"test.db");
    private readonly Clock clock=new();private const string Secret="whsec_synthetic_fixture_secret_0123456789",Scope="stripe-test:acct_fixture";
    private static readonly OrderOwner A=new("https://fixture.auth0.com/","reader-a"),B=new("https://fixture.auth0.com/","reader-b");
    private static string Key()=>Guid.NewGuid().ToString("N");
    private static OrderSnapshot Order(SqliteOrderStore store)
    {var q=store.CreateQuote(A,new SongRequest{Genre="Folk",Occasion="Invented occasion",Story="Synthetic story",ContactName="Sample Reader",Email="reader@example.test"});return store.Accept(A,new(q.Id,Key(),q.Request));}
    private VerifiedCheckoutEvent Event(CheckoutReservation r,string eventId="evt_fixture",string type="checkout.session.completed",string payment="paid",long? amount=null,string? session=null)
    {var bytes=Payload(r,eventId,type,payment,amount,session);return new StripeTestEventVerifier(Secret,Scope,clock).Verify(bytes,Sign(bytes,clock.GetUtcNow().ToUnixTimeSeconds()))!;}
    internal static byte[] Payload(CheckoutReservation r,string id="evt_fixture",string type="checkout.session.completed",string payment="paid",long? amount=null,string? session=null)=>JsonSerializer.SerializeToUtf8Bytes(new
    {id,@object="event",livemode=false,type,data=new{@object=new{id=session??"cs_test_"+r.AttemptId,@object="checkout.session",mode="payment",livemode=false,client_reference_id=r.AttemptId,metadata=new{order_id=r.OrderId},currency="usd",amount_total=amount??r.TotalMinor,payment_status=payment,payment_intent=payment=="paid"?"pi_fixture":null}}});
    internal static string Sign(byte[] bytes,long timestamp)
    {var prefix=Encoding.ASCII.GetBytes(timestamp+".");return "t="+timestamp+",v1="+Convert.ToHexString(HMACSHA256.HashData(Encoding.UTF8.GetBytes(Secret),prefix.Concat(bytes).ToArray())).ToLowerInvariant();}
    private CheckoutReservation Reserve(SqliteOrderStore store,OrderSnapshot order)=>store.ReserveTestCheckout(A,order.Id,Scope,"https://api.example.test/");
    private static HostedTestSession Session(CheckoutReservation r)=>new("cs_test_"+r.AttemptId,"https://checkout.stripe.com/c/pay/"+r.AttemptId);
    [Fact] public void Durable_reservation_reuses_attempt_and_cannot_cross_owner()
    {OrderSnapshot o;CheckoutReservation r;using(var s=new SqliteOrderStore(Db,clock)){o=Order(s);r=Reserve(s,o);Assert.Equal(r,Reserve(s,o));Assert.Throws<OrderRejectedException>(()=>s.ReserveTestCheckout(B,o.Id,Scope,r.ReturnOrigin));}using var reopened=new SqliteOrderStore(Db,clock);Assert.Equal(r,Reserve(reopened,o));}
    [Fact] public async Task Lost_provider_reply_reuses_same_server_key_and_session()
    {using var s=new SqliteOrderStore(Db,clock);var o=Order(s);var provider=new Provider{LoseReply=true};var flow=new TestCheckoutCoordinator(s,provider,clock);await Assert.ThrowsAsync<HttpRequestException>(()=>flow.CreateAsync(A,o.Id,Ct));var result=await flow.CreateAsync(A,o.Id,Ct);Assert.Equal(2,provider.Calls.Count);Assert.Equal(provider.Calls[0],provider.Calls[1]);Assert.Equal(TestPaymentState.Pending,result.State);}
    [Fact] public async Task Unresolved_provider_attempt_is_not_reposted_after_key_retention_window()
    {using var s=new SqliteOrderStore(Db,clock);var o=Order(s);Reserve(s,o);clock.Now=clock.Now.AddHours(23);var provider=new Provider();await Assert.ThrowsAsync<PaymentConflictException>(()=>new TestCheckoutCoordinator(s,provider,clock).CreateAsync(A,o.Id,Ct));Assert.Empty(provider.Calls);}
    [Fact] public void Verified_test_payment_commits_once_survives_reopen_and_gates_fixture_production()
    {OrderSnapshot o;TestPaymentReceipt paid;using(var s=new SqliteOrderStore(Db,clock)){o=Order(s);var r=Reserve(s,o);Assert.Throws<PaymentConflictException>(()=>s.StartProductionForPaymentLab(o.Id,1));s.AttachTestSession(r,Session(r));var e=Event(r);paid=s.ApplyVerifiedTestEvent(e);Assert.Equal(paid,s.ApplyVerifiedTestEvent(e));Assert.Equal(TestPaymentState.Paid,paid.State);Assert.Null(paid.CheckoutUrl);}using var reopened=new SqliteOrderStore(Db,clock);Assert.Equal(paid,reopened.GetTestPayment(A,o.Id));Assert.Equal(OrderState.InProduction,reopened.StartProductionForPaymentLab(o.Id,1).State);Assert.Throws<PaymentConflictException>(()=>reopened.StartProductionForPaymentLab(o.Id,1));}
    [Fact] public void Completed_unpaid_does_not_unlock_production_then_async_success_does()
    {using var s=new SqliteOrderStore(Db,clock);var o=Order(s);var r=Reserve(s,o);s.AttachTestSession(r,Session(r));Assert.Equal(TestPaymentState.Pending,s.ApplyVerifiedTestEvent(Event(r,payment:"unpaid")).State);Assert.Throws<PaymentConflictException>(()=>s.StartProductionForPaymentLab(o.Id,1));Assert.Equal(TestPaymentState.Paid,s.ApplyVerifiedTestEvent(Event(r,"evt_async","checkout.session.async_payment_succeeded")).State);}
    [Theory][InlineData("checkout.session.async_payment_failed")][InlineData("checkout.session.expired")][InlineData("checkout.session.completed")]
    public void Late_failure_expiry_or_unpaid_completed_never_downgrades_paid(string type)
    {using var s=new SqliteOrderStore(Db,clock);var r=Reserve(s,Order(s));s.AttachTestSession(r,Session(r));var paid=s.ApplyVerifiedTestEvent(Event(r));Assert.Equal(paid,s.ApplyVerifiedTestEvent(Event(r,"evt_late",type,"unpaid")));}
    [Fact] public void Failure_followed_by_verified_success_converges_to_paid()
    {using var s=new SqliteOrderStore(Db,clock);var r=Reserve(s,Order(s));s.AttachTestSession(r,Session(r));Assert.Equal(TestPaymentState.Failed,s.ApplyVerifiedTestEvent(Event(r,"evt_failed","checkout.session.async_payment_failed","unpaid")).State);Assert.Equal(TestPaymentState.Paid,s.ApplyVerifiedTestEvent(Event(r,"evt_paid")).State);}
    [Fact] public void Paid_after_cancellation_requires_review_and_cannot_start_fixture_production()
    {using var s=new SqliteOrderStore(Db,clock);var o=Order(s);var r=Reserve(s,o);s.AttachTestSession(r,Session(r));s.AdvanceForLab(o.Id,1,OrderState.Canceled);Assert.Equal(TestPaymentState.ReviewRequired,s.ApplyVerifiedTestEvent(Event(r)).State);Assert.Throws<PaymentConflictException>(()=>s.StartProductionForPaymentLab(o.Id,2));}
    [Theory][InlineData("amount")][InlineData("session")][InlineData("scope")][InlineData("event-collision")]
    public void Signed_mismatches_or_reused_event_id_with_changed_facts_do_not_change_payment(string field)
    {using var s=new SqliteOrderStore(Db,clock);var r=Reserve(s,Order(s));s.AttachTestSession(r,Session(r));var e=field switch{"amount"=>Event(r,amount:1),"session"=>Event(r,session:"cs_test_wrong"),"scope"=>Event(r with{Scope="unused"}),_=>Event(r)};
     if(field=="scope"){var bytes=Payload(r);e=new StripeTestEventVerifier(Secret,"stripe-test:acct_other",clock).Verify(bytes,Sign(bytes,clock.Now.ToUnixTimeSeconds()))!;}
     if(field=="event-collision"){s.ApplyVerifiedTestEvent(e);Assert.Throws<PaymentConflictException>(()=>s.ApplyVerifiedTestEvent(Event(r,payment:"unpaid")));}else{Assert.Throws<PaymentConflictException>(()=>s.ApplyVerifiedTestEvent(e));Assert.Equal(TestPaymentState.Pending,s.GetTestPayment(A,r.OrderId)!.State);}}
    [Fact] public void Event_before_session_attachment_is_retryable_and_not_acknowledged_as_processed()
    {using var s=new SqliteOrderStore(Db,clock);var r=Reserve(s,Order(s));var e=Event(r);Assert.Throws<PaymentConflictException>(()=>s.ApplyVerifiedTestEvent(e));s.AttachTestSession(r,Session(r));Assert.Equal(TestPaymentState.Paid,s.ApplyVerifiedTestEvent(e).State);}
    [Fact] public void Failure_during_payment_update_rolls_back_event_receipt_and_can_retry()
    {using var s=new SqliteOrderStore(Db,clock);var r=Reserve(s,Order(s));s.AttachTestSession(r,Session(r));using(var db=new SqliteConnection("Data Source="+Db+";Pooling=False")){db.Open();using var c=db.CreateCommand();c.CommandText="CREATE TRIGGER fail_paid BEFORE UPDATE ON commerce_payments BEGIN SELECT RAISE(ABORT,'synthetic failure'); END";c.ExecuteNonQuery();}var e=Event(r);Assert.Throws<SqliteException>(()=>s.ApplyVerifiedTestEvent(e));using(var db=new SqliteConnection("Data Source="+Db+";Pooling=False")){db.Open();using var c=db.CreateCommand();c.CommandText="DROP TRIGGER fail_paid";c.ExecuteNonQuery();}Assert.Equal(TestPaymentState.Paid,s.ApplyVerifiedTestEvent(e).State);}
    [Fact] public async Task Two_connections_processing_same_signed_event_converge()
    {using var a=new SqliteOrderStore(Db,clock);using var b=new SqliteOrderStore(Db,clock);var r=Reserve(a,Order(a));a.AttachTestSession(r,Session(r));var e=Event(r);var results=await Task.WhenAll(Task.Run(()=>a.ApplyVerifiedTestEvent(e),Ct),Task.Run(()=>b.ApplyVerifiedTestEvent(e),Ct));Assert.Equal(results[0],results[1]);}
    [Theory][InlineData("tamper")][InlineData("old")][InlineData("future")][InlineData("duplicate-timestamp")][InlineData("wrong-secret")]
    public void Invalid_signatures_or_stale_deliveries_are_rejected(string defect)
    {using var s=new SqliteOrderStore(Db,clock);var bytes=Payload(Reserve(s,Order(s)));var timestamp=clock.Now.ToUnixTimeSeconds()+(defect=="old"?-301:defect=="future"?301:0);var signature=Sign(bytes,timestamp);if(defect=="tamper")bytes=bytes.Concat(new byte[]{32}).ToArray();if(defect=="duplicate-timestamp")signature+=",t="+timestamp;var verifier=new StripeTestEventVerifier(defect=="wrong-secret"?"whsec_wrong_synthetic_secret_0123456789":Secret,Scope,clock);Assert.Throws<InvalidDataException>(()=>verifier.Verify(bytes,signature));}
    [Fact] public void Multiple_v1_signatures_allow_secret_rotation_and_unknown_event_has_no_effect()
    {using var s=new SqliteOrderStore(Db,clock);var r=Reserve(s,Order(s));var bytes=Payload(r);var signature=Sign(bytes,clock.Now.ToUnixTimeSeconds())+",v1="+new string('0',64);Assert.NotNull(new StripeTestEventVerifier(Secret,Scope,clock).Verify(bytes,signature));var unknown=Payload(r,type:"customer.created");Assert.Null(new StripeTestEventVerifier(Secret,Scope,clock).Verify(unknown,Sign(unknown,clock.Now.ToUnixTimeSeconds())));}
    [Theory][InlineData("livemode")][InlineData("connect")][InlineData("duplicate")]
    public void Signed_unsupported_environment_or_duplicate_fields_is_rejected(string defect)
    {using var s=new SqliteOrderStore(Db,clock);var text=Encoding.UTF8.GetString(Payload(Reserve(s,Order(s))));text=defect switch{"livemode"=>text.Replace("\"livemode\":false","\"livemode\":true"),"connect"=>"{\"account\":\"acct_other\","+text[1..],_=>"{\"id\":\"evt_duplicate\","+text[1..]};var bytes=Encoding.UTF8.GetBytes(text);Assert.Throws<InvalidDataException>(()=>new StripeTestEventVerifier(Secret,Scope,clock).Verify(bytes,Sign(bytes,clock.Now.ToUnixTimeSeconds())));}
    [Fact] public void Independently_generated_python_signature_vector_validates_raw_bytes()
    {var fixture=Path.Combine(AppContext.BaseDirectory,"Fixtures","payment-event.json");var bytes=File.ReadAllBytes(fixture);var signature=File.ReadAllText(Path.ChangeExtension(fixture,"signature.txt")).Trim();var e=new StripeTestEventVerifier(Secret,Scope,clock).Verify(bytes,signature);Assert.Equal("evt_pythonfixture",e!.EventId);Assert.Equal(17900,e.TotalMinor);}
    [Fact] public async Task Stripe_adapter_sends_server_price_and_stable_key_to_fixed_origin()
    {using var s=new SqliteOrderStore(Db,clock);var r=Reserve(s,Order(s));var calls=new List<string>();using var provider=new StripeTestCheckoutProvider("sk_test_synthetic_fixture_0123456789","acct_fixture",r.ReturnOrigin,new Handler(async request=>
      {Assert.Equal("api.stripe.com",request.RequestUri!.Host);Assert.Equal("2026-10-28.endive",Assert.Single(request.Headers.GetValues("Stripe-Version")));if(request.Method==HttpMethod.Get)return Json(new{id="acct_fixture"});calls.Add(await request.Content!.ReadAsStringAsync(Ct));Assert.Equal("gnazzo-test-"+r.AttemptId,Assert.Single(request.Headers.GetValues("Idempotency-Key")));return Json(new{id="cs_test_"+r.AttemptId,@object="checkout.session",livemode=false,mode="payment",client_reference_id=r.AttemptId,metadata=new{order_id=r.OrderId},currency="usd",amount_total=r.TotalMinor,url=Session(r).Url});}));
     Assert.Equal(Session(r),await provider.CreateAsync(r,Ct));Assert.Contains("17900",Assert.Single(calls));Assert.DoesNotContain("Synthetic+story",calls[0]);}
    [Theory][InlineData("sk_live_not_allowed_0123456789","https://api.example.test/")][InlineData("sk_test_synthetic_0123456789","http://localhost/")]
    public void Live_key_or_insecure_return_origin_is_rejected(string key,string origin)=>Assert.Throws<ArgumentException>(()=>new StripeTestCheckoutProvider(key,"acct_fixture",origin));
    [Fact] public void Checkout_URL_is_strict_and_receipts_redact_private_identifiers()
    {Assert.False(PaymentContract.CheckoutUrl("https://checkout.stripe.com.evil.example/a"));Assert.False(PaymentContract.CheckoutUrl("https://user@checkout.stripe.com/a"));Assert.DoesNotContain("private",new HostedTestSession("cs_test_private","https://checkout.stripe.com/private").ToString().Replace("private URL omitted","omitted"));}
    [Fact] public async Task Wrong_provider_account_stops_before_checkout_POST()
    {using var s=new SqliteOrderStore(Db,clock);var r=Reserve(s,Order(s));var calls=0;using var provider=new StripeTestCheckoutProvider("sk_test_synthetic_fixture_0123456789","acct_fixture",r.ReturnOrigin,new Handler(request=>{calls++;Assert.Equal(HttpMethod.Get,request.Method);return Task.FromResult(Json(new{id="acct_other"}));}));await Assert.ThrowsAsync<PaymentConflictException>(()=>provider.CreateAsync(r,Ct));Assert.Equal(1,calls);}
    [Theory][InlineData("amount")][InlineData("url")][InlineData("livemode")]
    public async Task Provider_response_must_match_amount_destination_and_test_environment(string defect)
    {using var s=new SqliteOrderStore(Db,clock);var r=Reserve(s,Order(s));using var provider=new StripeTestCheckoutProvider("sk_test_synthetic_fixture_0123456789","acct_fixture",r.ReturnOrigin,new Handler(request=>Task.FromResult(request.Method==HttpMethod.Get?Json(new{id="acct_fixture"}):Json(new{id="cs_test_"+r.AttemptId,@object="checkout.session",livemode=defect=="livemode",mode="payment",client_reference_id=r.AttemptId,metadata=new{order_id=r.OrderId},currency="usd",amount_total=defect=="amount"?1:r.TotalMinor,url=defect=="url"?"https://evil.example/":Session(r).Url}))));await Assert.ThrowsAsync<InvalidDataException>(()=>provider.CreateAsync(r,Ct));}
    [Fact] public void Different_payment_reference_for_a_paid_session_requires_reconciliation()
    {using var s=new SqliteOrderStore(Db,clock);var r=Reserve(s,Order(s));s.AttachTestSession(r,Session(r));s.ApplyVerifiedTestEvent(Event(r));var text=Encoding.UTF8.GetString(Payload(r,"evt_otherintent")).Replace("pi_fixture","pi_other");var bytes=Encoding.UTF8.GetBytes(text);var e=new StripeTestEventVerifier(Secret,Scope,clock).Verify(bytes,Sign(bytes,clock.Now.ToUnixTimeSeconds()))!;Assert.Throws<PaymentConflictException>(()=>s.ApplyVerifiedTestEvent(e));}
    private static HttpResponseMessage Json(object value)=>new(HttpStatusCode.OK){Content=new StringContent(JsonSerializer.Serialize(value),Encoding.UTF8,"application/json")};
    private sealed class Handler(Func<HttpRequestMessage,Task<HttpResponseMessage>> send):HttpMessageHandler{protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage r,CancellationToken ct)=>send(r);}
    private sealed class Provider:ITestCheckoutProvider
    {public string Scope=>PaymentTests.Scope;public string ReturnOrigin=>"https://api.example.test/";public List<string> Calls{get;}=[];public bool LoseReply{get;set;}
     public Task<HostedTestSession> CreateAsync(CheckoutReservation r,CancellationToken ct){Calls.Add(r.AttemptId);if(LoseReply){LoseReply=false;throw new HttpRequestException("Synthetic lost reply");}return Task.FromResult(Session(r));}}
    private sealed class Clock:TimeProvider{public DateTimeOffset Now=new(2026,11,16,6,0,0,TimeSpan.Zero);public override DateTimeOffset GetUtcNow()=>Now;}
    public void Dispose(){if(Directory.Exists(root))Directory.Delete(root,true);}
}

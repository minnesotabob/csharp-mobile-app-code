using System.Net;
using System.Text;
using System.Text.Json;
using GnazzosWorld.Core.Commerce;
using GnazzosWorld.Core.Identity;
using GnazzosWorld.Core.Orders;
using Microsoft.Data.Sqlite;
using Xunit;

namespace GnazzosWorld.Core.Tests;

public sealed class CommerceTests:IDisposable
{
    private static CancellationToken Ct=>TestContext.Current.CancellationToken;
    private readonly string root=Path.Combine(Path.GetTempPath(),"gnazzo-commerce-"+Guid.NewGuid().ToString("N"));
    private string ServerPath=>Path.Combine(root,"server.db"); private string ClientPath=>Path.Combine(root,"client.db");
    private readonly Clock clock=new();
    private static readonly OrderOwner A=new("https://fixture.auth0.com/","reader-a"), B=new("https://fixture.auth0.com/","reader-b");
    private static SongRequest Draft()=>new(){Genre="Folk",Occasion="Synthetic anniversary",Story="Invented reference",ContactName="Sample Reader",Email="reader@example.test"};
    private static string Key()=>Guid.NewGuid().ToString("N");
    private static AcceptOrderCommand Attempt(OrderQuote q,string? key=null)=>new(q.Id,key??Key(),q.Request);
    private static CommerceAccount Account(OrderOwner? owner=null,string origin="https://api.example.test/")=>new(owner??A,origin,"https://gnazzosworld.example/api");
    [Fact]
    public void Quote_uses_canonical_request_and_server_minor_unit_price()
    {
        using var db=new SqliteOrderStore(ServerPath,clock);var q=db.CreateQuote(A,Draft() with{Genre=" Folk ",Story=" Invented reference "});
        Assert.Equal("Folk",q.Request.Genre);Assert.Equal(17900,q.TotalMinor);Assert.Equal("USD",q.Currency);Assert.Equal(OrderContract.Fingerprint(Draft()),q.RequestHash);
    }
    [Fact]
    public void Accepted_price_snapshot_survives_later_pricing_change()
    {
        var pricing=new Pricing();using var db=new SqliteOrderStore(ServerPath,clock,pricing);var q=db.CreateQuote(A,Draft());pricing.Total=99999;pricing.Tag="lab-new";
        var order=db.Accept(A,Attempt(q));Assert.Equal(17900,order.TotalMinor);Assert.Equal("lab-old",order.PricingVersion);Assert.Equal(99999,db.CreateQuote(A,Draft()).TotalMinor);
    }
    [Fact]
    public void Acceptance_replay_survives_expiry_and_database_reopen()
    {
        AcceptOrderCommand command;OrderSnapshot first;
        using(var db=new SqliteOrderStore(ServerPath,clock)){var q=db.CreateQuote(A,Draft());command=Attempt(q);first=db.Accept(A,command);}
        clock.Now=clock.Now.AddDays(1);using var reopened=new SqliteOrderStore(ServerPath,clock);Assert.Equal(first,reopened.Accept(A,command));Assert.Single(reopened.HistoryForLab(first.Id));
    }
    [Fact]
    public void Expired_unaccepted_quote_is_rejected()
    {using var db=new SqliteOrderStore(ServerPath,clock);var q=db.CreateQuote(A,Draft());clock.Now=q.ExpiresAt;Assert.Equal(OrderProblem.ExpiredQuote,Assert.Throws<OrderRejectedException>(()=>db.Accept(A,Attempt(q))).Problem);}
    [Fact]
    public void Same_operation_key_cannot_change_its_request_or_quote()
    {
        using var db=new SqliteOrderStore(ServerPath,clock);var q=db.CreateQuote(A,Draft());var key=Key();db.Accept(A,Attempt(q,key));var next=db.CreateQuote(A,Draft());
        Assert.Equal(OrderProblem.KeyConflict,Assert.Throws<OrderRejectedException>(()=>db.Accept(A,Attempt(next,key))).Problem);
    }
    [Fact]
    public void Changed_request_cannot_accept_an_unchanged_quote()
    {using var db=new SqliteOrderStore(ServerPath,clock);var q=db.CreateQuote(A,Draft());Assert.Equal(OrderProblem.ChangedRequest,Assert.Throws<OrderRejectedException>(()=>db.Accept(A,Attempt(q) with{Request=q.Request with{Quantity=2}})).Problem);}
    [Fact]
    public void A_quote_cannot_create_a_second_order_with_a_fresh_key()
    {using var db=new SqliteOrderStore(ServerPath,clock);var q=db.CreateQuote(A,Draft());db.Accept(A,Attempt(q));Assert.Equal(OrderProblem.QuoteUsed,Assert.Throws<OrderRejectedException>(()=>db.Accept(A,Attempt(q))).Problem);}
    [Fact]
    public void Key_namespace_is_scoped_to_validated_owner()
    {using var db=new SqliteOrderStore(ServerPath,clock);var key=Key();var a=db.Accept(A,Attempt(db.CreateQuote(A,Draft()),key));var b=db.Accept(B,Attempt(db.CreateQuote(B,Draft()),key));Assert.NotEqual(a.Id,b.Id);}
    [Fact]
    public void Foreign_quote_and_order_are_unavailable()
    {
        using var db=new SqliteOrderStore(ServerPath,clock);var q=db.CreateQuote(A,Draft());Assert.Equal(OrderProblem.Unavailable,Assert.Throws<OrderRejectedException>(()=>db.Accept(B,Attempt(q))).Problem);
        var order=db.Accept(A,Attempt(q));Assert.Null(db.Get(B,order.Id));Assert.Null(db.Get(new("https://other.auth0.com/",A.Subject),order.Id));
    }
    [Theory]
    [InlineData(0,1)] [InlineData(5,1)] [InlineData(3,0)] [InlineData(3,4)]
    public void Invalid_specifications_do_not_produce_quotes(int minutes,int quantity)
    {using var db=new SqliteOrderStore(ServerPath,clock);Assert.Equal(OrderProblem.InvalidRequest,Assert.Throws<OrderRejectedException>(()=>db.CreateQuote(A,Draft() with{Minutes=minutes,Quantity=quantity})).Problem);}
    [Fact]
    public void Null_and_control_bearing_fields_are_rejected_without_null_dereference()
    {Assert.Throws<OrderRejectedException>(()=>OrderContract.Normalize(Draft() with{Story=null!}));Assert.Throws<OrderRejectedException>(()=>OrderContract.Normalize(Draft() with{Story="bad\0input"}));}
    [Fact]
    public async Task Independent_sqlite_connections_serialize_duplicate_acceptance()
    {
        using var first=new SqliteOrderStore(ServerPath,clock);using var second=new SqliteOrderStore(ServerPath,clock);var q=first.CreateQuote(A,Draft());var command=Attempt(q);
        var results=await Task.WhenAll(Task.Run(()=>first.Accept(A,command),Ct),Task.Run(()=>second.Accept(A,command),Ct));Assert.Equal(results[0].Id,results[1].Id);Assert.Single(first.HistoryForLab(results[0].Id));
    }
    [Fact]
    public async Task Independent_writers_with_different_keys_cannot_consume_the_quote_twice()
    {
        using var first=new SqliteOrderStore(ServerPath,clock);using var second=new SqliteOrderStore(ServerPath,clock);var q=first.CreateQuote(A,Draft());
        bool Accept(SqliteOrderStore store){try{store.Accept(A,Attempt(q));return true;}catch(OrderRejectedException ex){Assert.Equal(OrderProblem.QuoteUsed,ex.Problem);return false;}}
        var results=await Task.WhenAll(Task.Run(()=>Accept(first),Ct),Task.Run(()=>Accept(second),Ct));Assert.Single(results,x=>x);
    }
    [Fact]
    public void History_failure_rolls_back_order_and_quote_can_be_retried()
    {
        using var db=new SqliteOrderStore(ServerPath,clock);var q=db.CreateQuote(A,Draft());var command=Attempt(q);
        using var inspect=new SqliteConnection(new SqliteConnectionStringBuilder{DataSource=ServerPath,Pooling=false}.ToString());inspect.Open();using var sql=inspect.CreateCommand();
        sql.CommandText="CREATE TRIGGER reject_history BEFORE INSERT ON commerce_history BEGIN SELECT RAISE(ABORT,'synthetic failure'); END;";sql.ExecuteNonQuery();Assert.Throws<SqliteException>(()=>db.Accept(A,command));
        sql.CommandText="SELECT COUNT(*) FROM commerce_orders";Assert.Equal(0,(long)sql.ExecuteScalar()!);sql.CommandText="DROP TRIGGER reject_history";sql.ExecuteNonQuery();Assert.Equal(OrderState.Accepted,db.Accept(A,command).State);
    }
    [Fact]
    public void Trusted_fixture_transitions_preserve_price_and_append_versioned_history()
    {
        using var db=new SqliteOrderStore(ServerPath,clock);var order=db.Accept(A,Attempt(db.CreateQuote(A,Draft())));var original=order;
        foreach(var state in new[]{OrderState.InProduction,OrderState.ReadyForReview,OrderState.InProduction,OrderState.ReadyForReview,OrderState.Delivered})order=db.AdvanceForLab(order.Id,order.Version,state);
        Assert.Equal(6,order.Version);Assert.Equal(original.TotalMinor,order.TotalMinor);Assert.Equal(6,db.HistoryForLab(order.Id).Count);
        Assert.Equal(OrderProblem.InvalidTransition,Assert.Throws<OrderRejectedException>(()=>db.AdvanceForLab(order.Id,order.Version,OrderState.InProduction)).Problem);
    }
    [Fact]
    public void Stale_or_invalid_transition_leaves_record_unchanged()
    {
        using var db=new SqliteOrderStore(ServerPath,clock);var order=db.Accept(A,Attempt(db.CreateQuote(A,Draft())));
        Assert.Equal(OrderProblem.InvalidTransition,Assert.Throws<OrderRejectedException>(()=>db.AdvanceForLab(order.Id,1,OrderState.Delivered)).Problem);
        var next=db.AdvanceForLab(order.Id,1,OrderState.InProduction);Assert.Equal(OrderProblem.VersionConflict,Assert.Throws<OrderRejectedException>(()=>db.AdvanceForLab(order.Id,1,OrderState.ReadyForReview)).Problem);Assert.Equal(next,db.Get(A,order.Id));
    }
    [Fact]
    public void Canceled_order_is_terminal_in_the_controlled_fixture()
    {using var db=new SqliteOrderStore(ServerPath,clock);var order=db.Accept(A,Attempt(db.CreateQuote(A,Draft())));db.AdvanceForLab(order.Id,1,OrderState.Canceled);Assert.Throws<OrderRejectedException>(()=>db.AdvanceForLab(order.Id,2,OrderState.InProduction));}
    [Fact]
    public void Quote_capacity_is_bounded_for_the_teaching_owner()
    {using var db=new SqliteOrderStore(ServerPath,clock);for(var i=0;i<100;i++)db.CreateQuote(A,Draft());Assert.Equal(OrderProblem.Capacity,Assert.Throws<OrderRejectedException>(()=>db.CreateQuote(A,Draft())).Problem);}
    [Fact]
    public void Local_operation_is_persisted_and_isolated_by_owner_and_resource()
    {
        using var server=new SqliteOrderStore(ServerPath,clock);var q=server.CreateQuote(A,Draft());PendingOrderOperation operation;
        using(var db=new PendingOrderDatabase(ClientPath)){operation=db.Begin(Account(),q);Assert.Equal(operation,db.Begin(Account(),q));}
        using var reopened=new PendingOrderDatabase(ClientPath);Assert.Equal(operation,reopened.Read(Account()));Assert.Null(reopened.Read(Account(B)));Assert.Null(reopened.Read(Account(origin:"https://other.example.test/")));
    }
    [Fact]
    public void An_unresolved_operation_blocks_a_different_quote()
    {using var server=new SqliteOrderStore(ServerPath,clock);using var pending=new PendingOrderDatabase(ClientPath);pending.Begin(Account(),server.CreateQuote(A,Draft()));Assert.Throws<InvalidOperationException>(()=>pending.Begin(Account(),server.CreateQuote(A,Draft())));}
    [Fact]
    public void Definitive_expiry_permits_a_new_quote_but_generic_bad_request_does_not()
    {
        using var server=new SqliteOrderStore(ServerPath,clock);using var pending=new PendingOrderDatabase(ClientPath);var p=pending.Begin(Account(),server.CreateQuote(A,Draft()));
        Assert.False(PendingOrderDatabase.Definitive(OrderProblem.InvalidRequest));Assert.Throws<InvalidOperationException>(()=>pending.Reject(Account(),p.OperationKey,OrderProblem.InvalidRequest));
        pending.Reject(Account(),p.OperationKey,OrderProblem.ExpiredQuote);Assert.NotEqual(p.OperationKey,pending.Begin(Account(),server.CreateQuote(A,Draft())).OperationKey);
    }
    [Fact]
    public void Confirmed_receipt_survives_reopen_and_cannot_regress_version()
    {
        using var server=new SqliteOrderStore(ServerPath,clock);var q=server.CreateQuote(A,Draft());OrderSnapshot order;
        using(var p=new PendingOrderDatabase(ClientPath)){var operation=p.Begin(Account(),q);order=server.Accept(A,Attempt(q,operation.OperationKey));p.Confirm(Account(),operation.OperationKey,order);var newer=server.AdvanceForLab(order.Id,1,OrderState.InProduction);p.Confirm(Account(),operation.OperationKey,newer);Assert.Throws<InvalidDataException>(()=>p.Confirm(Account(),operation.OperationKey,order));}
        using var reopened=new PendingOrderDatabase(ClientPath);Assert.Equal(2,reopened.Read(Account())!.Confirmed!.Version);
    }
    [Fact]
    public void Private_request_records_are_redacted_in_diagnostic_text()
    {using var server=new SqliteOrderStore(ServerPath,clock);var q=server.CreateQuote(A,Draft());Assert.DoesNotContain(Draft().Story,q.ToString());Assert.DoesNotContain(Draft().Email,Attempt(q).ToString());}
    [Fact]
    public async Task Failed_client_persistence_prevents_acceptance_HTTP()
    {
        using var server=new SqliteOrderStore(ServerPath,clock);using var pending=new PendingOrderDatabase(ClientPath);var session=Session();var lease=await session.AcquireOwnedAsync(Ct);var calls=0;
        using var api=new OrderApiClient(session,new Handler((_,_)=>{calls++;throw new IOException();}));var flow=new OrderAttemptCoordinator(session,api,pending);
        using var inspect=new SqliteConnection(new SqliteConnectionStringBuilder{DataSource=ClientPath,Pooling=false}.ToString());inspect.Open();using var command=inspect.CreateCommand();command.CommandText="CREATE TRIGGER fail_pending BEFORE INSERT ON pending_order_operations BEGIN SELECT RAISE(ABORT,'synthetic failure');END";command.ExecuteNonQuery();
        await Assert.ThrowsAsync<SqliteException>(()=>flow.AcceptAsync(server.CreateQuote(A,Draft()),lease,Ct));Assert.Equal(0,calls);
    }
    [Fact]
    public async Task Lost_response_then_process_reopen_retries_the_original_operation()
    {
        var session=Session();var lease=await session.AcquireOwnedAsync(Ct);OrderQuote quote;string key;
        using(var server=new SqliteOrderStore(ServerPath,clock))
        using(var pending=new PendingOrderDatabase(ClientPath))
        using(var api=new OrderApiClient(session,new Handler(async(request,ct)=>
        {
            var command=JsonSerializer.Deserialize<AcceptOrderCommand>(await request.Content!.ReadAsStringAsync(ct),OrderContract.Json)!;
            Assert.Equal(command.OperationKey,pending.Read(Account())!.OperationKey);server.Accept(A,command);throw new HttpRequestException("Synthetic reply loss.");
        })))
        {quote=server.CreateQuote(A,Draft());var flow=new OrderAttemptCoordinator(session,api,pending);await Assert.ThrowsAsync<HttpRequestException>(()=>flow.AcceptAsync(quote,lease,Ct));key=pending.Read(Account())!.OperationKey;}
        clock.Now=clock.Now.AddDays(1);var nextSession=Session();var nextLease=await nextSession.AcquireOwnedAsync(Ct);
        using var reopenedServer=new SqliteOrderStore(ServerPath,clock);using var reopenedPending=new PendingOrderDatabase(ClientPath);
        using var resumedApi=new OrderApiClient(nextSession,new Handler(async(request,ct)=>Response(reopenedServer.Accept(A,JsonSerializer.Deserialize<AcceptOrderCommand>(await request.Content!.ReadAsStringAsync(ct),OrderContract.Json)!))));
        var result=await new OrderAttemptCoordinator(nextSession,resumedApi,reopenedPending).ResumeAsync(nextLease,Ct);
        Assert.Equal(key,result.OperationKey);Assert.NotNull(reopenedPending.Read(Account())!.Confirmed);Assert.Single(reopenedServer.HistoryForLab(result.Id));
    }
    [Fact]
    public async Task Signout_fences_a_late_acceptance_reply_without_discarding_original_owner_operation()
    {
        using var server=new SqliteOrderStore(ServerPath,clock);using var pending=new PendingOrderDatabase(ClientPath);var session=Session();var lease=await session.AcquireOwnedAsync(Ct);
        var accepted=new TaskCompletionSource<OrderSnapshot>(TaskCreationOptions.RunContinuationsAsynchronously);var release=new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        using var api=new OrderApiClient(session,new Handler(async(request,ct)=>
        {var command=JsonSerializer.Deserialize<AcceptOrderCommand>(await request.Content!.ReadAsStringAsync(ct),OrderContract.Json)!;var result=server.Accept(A,command);accepted.SetResult(result);await release.Task;return Response(result);}));
        var operation=new OrderAttemptCoordinator(session,api,pending).AcceptAsync(server.CreateQuote(A,Draft()),lease,Ct);
        var created=await accepted.Task.WaitAsync(Ct);await session.SignOutLocalAsync(Ct);release.SetResult();await Assert.ThrowsAsync<SessionEndedException>(()=>operation);
        Assert.Null(pending.Read(Account())!.Confirmed);Assert.Null(pending.Read(Account(B)));Assert.NotNull(server.Get(A,created.Id));
    }
    private ApiSession Session()
    {
        var settings=new ApiSessionSettings(new("fixture.auth0.com","public-client-id"),"https://gnazzosworld.example/api",new("https://api.example.test/"));
        return new(settings,new Vault(new(settings.Identity.Issuer,A.Subject,settings.Audience,"synthetic-access",null,clock.Now.AddMinutes(10))),new Provider(),clock);
    }
    private static HttpResponseMessage Response(OrderSnapshot order)=>new(HttpStatusCode.OK){Content=new StringContent(JsonSerializer.Serialize(order,OrderContract.Json),Encoding.UTF8,"application/json")};
    private sealed class Handler(Func<HttpRequestMessage,CancellationToken,Task<HttpResponseMessage>> send):HttpMessageHandler
    {protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage r,CancellationToken ct)=>send(r,ct);}
    private sealed class Vault(SessionGrant initial):ISessionVault
    {private SessionGrant? value=initial;public Task<SessionGrant?> ReadAsync(CancellationToken ct)=>Task.FromResult(value);public Task WriteAsync(SessionGrant? grant,CancellationToken ct){value=grant;return Task.CompletedTask;}}
    private sealed class Provider:ISessionProvider
    {public Task<SessionGrant> SignInAsync(CancellationToken ct)=>throw new InvalidOperationException();public Task<SessionGrant> RefreshAsync(SessionGrant old,CancellationToken ct)=>throw new InvalidOperationException();}
    private sealed class Clock:TimeProvider
    {public DateTimeOffset Now=new(2026,11,15,12,0,0,TimeSpan.Zero);public override DateTimeOffset GetUtcNow()=>Now;}
    private sealed class Pricing:IOrderPricing
    {public long Total=17900;public string Tag="lab-old";public string Version=>Tag;public long TotalMinor(SongRequest r)=>Total;}
    public void Dispose(){if(Directory.Exists(root))Directory.Delete(root,recursive:true);}
}

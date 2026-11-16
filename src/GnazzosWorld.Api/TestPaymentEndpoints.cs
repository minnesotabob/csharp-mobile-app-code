using GnazzosWorld.Core.Commerce;
using GnazzosWorld.Core.Payments;
using Microsoft.AspNetCore.RateLimiting;

namespace GnazzosWorld.Api;

public static class TestPaymentEndpoints
{
    public static void Register(WebApplicationBuilder builder)
    {
        builder.Services.AddSingleton<ITestCheckoutProvider>(_=>new StripeTestCheckoutProvider(
            builder.Configuration["Payments:TestSecretKey"]??"",builder.Configuration["Payments:AccountId"]??"",builder.Configuration["Payments:ReturnOrigin"]??""));
        builder.Services.AddSingleton(sp=>new StripeTestEventVerifier(builder.Configuration["Payments:WebhookSecret"]??"",sp.GetRequiredService<ITestCheckoutProvider>().Scope,sp.GetRequiredService<TimeProvider>()));
        builder.Services.AddSingleton<TestCheckoutCoordinator>();
        builder.Services.AddRateLimiter(o=>o.AddFixedWindowLimiter("test-webhook-budget",l=>{l.PermitLimit=100;l.Window=TimeSpan.FromMinutes(1);l.QueueLimit=0;}));
    }
    private static OrderOwner Owner(HttpContext c)=>new(c.User.FindFirst("iss")!.Value,c.User.FindFirst("sub")!.Value);
    private static IResult Unavailable()=>Results.Problem(statusCode:404,title:"The owned test payment is unavailable.");
    public static void Map(WebApplication app)
    {
        if(!app.Configuration.GetValue<bool>("Payments:Enabled"))return;
        if(!app.Environment.IsDevelopment()||!app.Configuration.GetValue<bool>("Commerce:Enabled"))throw new InvalidOperationException("Test payments require Development and the synthetic commerce lab.");
        _=app.Services.GetRequiredService<ITestCheckoutProvider>();_=app.Services.GetRequiredService<StripeTestEventVerifier>();
        app.MapPost("/api/v1/commerce/orders/{id}/test-checkout",async(string id,HttpContext c,TestCheckoutCoordinator coordinator)=>
        {
            // No browser-provided price, return URL, owner, payment status or card body.
            if(await c.Request.Body.ReadAsync(new byte[1],c.RequestAborted)!=0)return Results.Problem(statusCode:400,title:"This test operation accepts no body.");
            try{return Results.Json(await coordinator.CreateAsync(Owner(c),id,c.RequestAborted),OrderContract.Json);}
            catch(OrderRejectedException){return Unavailable();}
            catch(Exception ex) when(ex is PaymentConflictException or HttpRequestException or InvalidDataException or OperationCanceledException){return Results.Problem(statusCode:503,title:"Test checkout is unconfirmed. Retain the existing server attempt.");}
        }).RequireAuthorization(CommerceEndpoints.WritePolicy).RequireRateLimiting("commerce-budget");
        app.MapGet("/api/v1/commerce/orders/{id}/test-payment",(string id,HttpContext c,SqliteOrderStore store)=>
        {try{var payment=store.GetTestPayment(Owner(c),id);return payment is null?Unavailable():Results.Json(payment,OrderContract.Json);}catch(OrderRejectedException){return Unavailable();}})
            .RequireAuthorization(CommerceEndpoints.ReadPolicy).RequireRateLimiting("commerce-budget");
        app.MapPost("/api/v1/commerce/test-payments/webhook",async(HttpContext c,StripeTestEventVerifier verifier,SqliteOrderStore store)=>
        {
            if(c.Request.Headers["Stripe-Signature"].Count!=1)return Results.Problem(statusCode:400,title:"Invalid test event.");
            var bytes=new byte[65537];var length=0;
            while(length<bytes.Length){var n=await c.Request.Body.ReadAsync(bytes.AsMemory(length),c.RequestAborted);if(n==0)break;length+=n;}
            if(length>65536)return Results.Problem(statusCode:400,title:"Invalid test event.");
            VerifiedCheckoutEvent? verified;
            try{verified=verifier.Verify(bytes.AsSpan(0,length),c.Request.Headers["Stripe-Signature"].ToString());}
            catch(InvalidDataException){return Results.Problem(statusCode:400,title:"Invalid test event.");}
            if(verified is null)return Results.Ok(); // Authenticated unsupported event; no state effect.
            try{store.ApplyVerifiedTestEvent(verified);return Results.Ok();}
            catch(PaymentConflictException){return Results.Problem(statusCode:503,title:"Test event needs reconciliation.");}
        }).AllowAnonymous().RequireRateLimiting("test-webhook-budget");
        foreach(var path in new[]{"/payment-test/return","/payment-test/cancel"})
            app.MapGet(path,(HttpContext c)=>{c.Response.Headers.CacheControl="no-store";return Results.Text("TEST ONLY. Returning or canceling does not establish payment. Read the owned server payment status in the lab.");});
    }
}

using System.Security.Cryptography;
using GnazzosWorld.Core.Commerce;
using GnazzosWorld.Core.Fulfillment;

namespace GnazzosWorld.Api;

public static class FulfillmentEndpoints
{
    private static OrderOwner Owner(HttpContext c)=>new(c.User.FindFirst("iss")!.Value,c.User.FindFirst("sub")!.Value);
    private static IResult Rejected(OrderProblem p)=>Results.Problem(statusCode:p switch{OrderProblem.InvalidRequest=>400,OrderProblem.Unavailable=>404,OrderProblem.Capacity=>503,_=>409},title:"The owned tracking operation was not completed.",extensions:new Dictionary<string,object?>{{"code",p.ToString()}});
    public static void Map(WebApplication app)
    {
        if(!app.Configuration.GetValue<bool>("Fulfillment:Enabled"))return;
        if(!app.Environment.IsDevelopment()||!app.Configuration.GetValue<bool>("Commerce:Enabled"))throw new InvalidOperationException("Fulfillment lab requires Development and synthetic commerce.");
        using var stream=typeof(FulfillmentEndpoints).Assembly.GetManifestResourceStream("PrivateDeliverySample.wav")??throw new InvalidDataException("Missing private fixture.");using var buffer=new MemoryStream();stream.CopyTo(buffer);var sample=buffer.ToArray();
        if(sample.LongLength!=PrivateDeliveryFixture.Length||Convert.ToHexString(SHA256.HashData(sample)).ToLowerInvariant()!=PrivateDeliveryFixture.Sha256)throw new InvalidDataException("Private sample does not match descriptor.");
        var read=CommerceEndpoints.ReadPolicy;var write=CommerceEndpoints.WritePolicy;
        app.MapGet("/api/v1/commerce/orders",(HttpContext c,SqliteOrderStore store)=>
        {if(c.Request.Query.Keys.Any(k=>k!="after")||c.Request.Query["after"].Count>1)return Rejected(OrderProblem.InvalidRequest);
         try{return Results.Json(store.ListOrders(Owner(c),c.Request.Query.TryGetValue("after",out var after)?after.ToString():null),OrderContract.Json);}catch(OrderRejectedException ex){return Rejected(ex.Problem);}}).RequireAuthorization(read).RequireRateLimiting("commerce-budget");
        app.MapGet("/api/v1/commerce/orders/{id}/tracking",(string id,HttpContext c,SqliteOrderStore store)=>
        {try{return Results.Json(store.TrackOrder(Owner(c),id),OrderContract.Json);}catch(OrderRejectedException ex){return Rejected(ex.Problem);}}).RequireAuthorization(read).RequireRateLimiting("commerce-budget");
        app.MapPost("/api/v1/commerce/orders/{id}/revisions",async(string id,HttpContext c,SqliteOrderStore store)=>
        {var command=await CommerceEndpoints.Body<RevisionCommand>(c);if(command is null)return Rejected(OrderProblem.InvalidRequest);
         try{return Results.Json(store.RequestRevision(Owner(c),id,command),OrderContract.Json);}catch(OrderRejectedException ex){return Rejected(ex.Problem);}}).RequireAuthorization(write).RequireRateLimiting("commerce-budget");
        app.MapPost("/api/v1/commerce/orders/{id}/deliveries/{delivery}/grant",async(string id,string delivery,HttpContext c,SqliteOrderStore store)=>
        {var command=await CommerceEndpoints.Body<GrantCommand>(c);if(command is null)return Rejected(OrderProblem.InvalidRequest);
         try{return Results.Json(store.IssueDeliveryGrant(Owner(c),id,delivery,command.ExpectedVersion),OrderContract.Json);}catch(OrderRejectedException ex){return Rejected(ex.Problem);}}).RequireAuthorization(read).RequireRateLimiting("commerce-budget");
        app.MapGet("/api/v1/commerce/orders/{id}/deliveries/{delivery}/content",(string id,string delivery,HttpContext c,SqliteOrderStore store)=>
        {
            if(c.Request.Query.Count!=0)return Rejected(OrderProblem.InvalidRequest);
            if(c.Request.Headers["X-Delivery-Grant"].Count!=1)return Rejected(OrderProblem.Unavailable);
            try{store.AuthorizeDelivery(Owner(c),id,delivery,c.Request.Headers["X-Delivery-Grant"].ToString());}
            catch(OrderRejectedException ex){return Rejected(ex.Problem);}
            if(c.Request.Headers.ContainsKey("Range"))return Rejected(OrderProblem.InvalidRequest); // This small complete-body lab has no private range cache.
            c.Response.Headers["X-Content-Type-Options"]="nosniff";
            return Results.File(sample,"audio/wav",fileDownloadName:"synthetic-delivery.wav",enableRangeProcessing:false);
        }).RequireAuthorization(read).RequireRateLimiting("commerce-budget");
        // No arbitrary file path, public static mapping, customer publication or administrative endpoint.
    }
    public sealed record GrantCommand(long ExpectedVersion);
}

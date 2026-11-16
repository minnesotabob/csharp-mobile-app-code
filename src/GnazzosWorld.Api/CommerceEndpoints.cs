using System.Security.Claims;
using System.Text.Json;
using GnazzosWorld.Core.Commerce;
using Microsoft.AspNetCore.RateLimiting;

namespace GnazzosWorld.Api;

public static class CommerceEndpoints
{
    public const string ReadPolicy="orders-read", WritePolicy="orders-write";
    public static void Register(WebApplicationBuilder builder)
    {
        builder.Services.AddSingleton(sp => new SqliteOrderStore(
            builder.Configuration["Commerce:Database"] ?? Path.Combine(AppContext.BaseDirectory,"lab-data","commerce-v1.sqlite3"),
            sp.GetRequiredService<TimeProvider>()));
        builder.Services.AddAuthorizationBuilder()
            .AddPolicy(ReadPolicy,p=>p.RequireAuthenticatedUser().RequireAssertion(c=>Allowed(c.User,"orders:read")))
            .AddPolicy(WritePolicy,p=>p.RequireAuthenticatedUser().RequireAssertion(c=>Allowed(c.User,"orders:write")));
        builder.Services.AddRateLimiter(o=>o.AddFixedWindowLimiter("commerce-budget",l=>
        { l.PermitLimit=20;l.Window=TimeSpan.FromMinutes(1);l.QueueLimit=0;l.AutoReplenishment=true; }));
    }
    private static bool Allowed(ClaimsPrincipal user,string scope)
    {
        var subjects=user.FindAll("sub").ToArray(); var issuers=user.FindAll("iss").ToArray();
        return subjects.Length==1 && !string.IsNullOrWhiteSpace(subjects[0].Value) && subjects[0].Value.Length<=256 && !subjects[0].Value.Any(char.IsControl) &&
            issuers.Length==1 && Uri.TryCreate(issuers[0].Value,UriKind.Absolute,out var uri) && uri.Scheme=="https" && uri.UserInfo.Length==0 && issuers[0].Value.Length<=256 &&
            user.FindAll("scope").SelectMany(c=>c.Value.Split(' ',StringSplitOptions.RemoveEmptyEntries)).Contains(scope,StringComparer.Ordinal);
    }
    private static OrderOwner Owner(ClaimsPrincipal user)=>new(user.FindFirst("iss")!.Value,user.FindFirst("sub")!.Value);
    private static IResult Rejected(OrderProblem problem)=>Results.Problem(statusCode:problem switch
    { OrderProblem.InvalidRequest=>400,OrderProblem.Unavailable=>404,OrderProblem.Capacity=>503,_=>409 },
        title:"The quote/order operation was not accepted.",extensions:new Dictionary<string,object?>{{"code",problem.ToString()}});
    private static async Task<T?> Body<T>(HttpContext context) where T:class
    {
        if (!System.Net.Http.Headers.MediaTypeHeaderValue.TryParse(context.Request.ContentType,out var type) || type.MediaType!="application/json") return default;
        var bytes=new byte[16385]; var length=0;
        while(length<bytes.Length){var n=await context.Request.Body.ReadAsync(bytes.AsMemory(length),context.RequestAborted);if(n==0)break;length+=n;}
        if(length>16384)return default;
        try
        {
            using var doc=JsonDocument.Parse(bytes.AsMemory(0,length),new JsonDocumentOptions{MaxDepth=8});
            bool Unique(JsonElement e)=> e.ValueKind!=JsonValueKind.Object ||
                (e.EnumerateObject().Select(p=>p.Name).Distinct(StringComparer.OrdinalIgnoreCase).Count()==e.EnumerateObject().Count() && e.EnumerateObject().All(p=>Unique(p.Value)));
            if(!Unique(doc.RootElement))return default;
            return JsonSerializer.Deserialize<T>(bytes.AsSpan(0,length),OrderContract.Json);
        }
        catch(JsonException){return default;}
    }
    public static void Map(WebApplication app)
    {
        if(!app.Configuration.GetValue<bool>("Commerce:Enabled"))return; // Opt-in synthetic teaching writes only.
        app.MapPost("/api/v1/commerce/quotes",async(HttpContext c,SqliteOrderStore store)=>
        {
            var command=await Body<QuoteCommand>(c);if(command is null)return Rejected(OrderProblem.InvalidRequest);
            try{return Results.Json(store.CreateQuote(Owner(c.User),command.Request),OrderContract.Json);}
            catch(OrderRejectedException ex){return Rejected(ex.Problem);}
        }).RequireAuthorization(WritePolicy).RequireRateLimiting("commerce-budget");
        app.MapPost("/api/v1/commerce/orders",async(HttpContext c,SqliteOrderStore store)=>
        {
            var command=await Body<AcceptOrderCommand>(c);if(command is null)return Rejected(OrderProblem.InvalidRequest);
            try{return Results.Json(store.Accept(Owner(c.User),command),OrderContract.Json);}
            catch(OrderRejectedException ex){return Rejected(ex.Problem);}
        }).RequireAuthorization(WritePolicy).RequireRateLimiting("commerce-budget");
        app.MapGet("/api/v1/commerce/orders/{id}",(string id,HttpContext c,SqliteOrderStore store)=>
        {
            try{var order=store.Get(Owner(c.User),id);return order is null?Rejected(OrderProblem.Unavailable):Results.Json(order,OrderContract.Json);}
            catch(OrderRejectedException ex){return Rejected(ex.Problem);}
        }).RequireAuthorization(ReadPolicy).RequireRateLimiting("commerce-budget");
        // No customer state/price/payment update endpoint. AdvanceForLab is a trusted controlled fixture only.
    }
}

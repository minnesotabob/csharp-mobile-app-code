using System.Security.Cryptography;
using GnazzosWorld.Core.Fulfillment;
using GnazzosWorld.Core.Operations;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.Extensions.Logging.Console;

namespace GnazzosWorld.Api.Operations;

public static class OperationsEndpoints
{
    public static void Register(WebApplicationBuilder builder)
    {
        if(!builder.Configuration.GetValue<bool>("Operations:Enabled"))return;
        var settings=OperationsSettings.Read(builder.Configuration,builder.Environment);builder.Services.AddSingleton(settings);builder.Services.AddSingleton<OperationTelemetry>();
        builder.Logging.AddJsonConsole(o=>{o.IncludeScopes=false;o.UseUtcTimestamp=true;o.TimestampFormat="O";});
        // A final options callback prevents more-specific framework categories from reopening raw request/exception logs.
        builder.Services.PostConfigure<LoggerFilterOptions>(o=>{o.Rules.Clear();o.MinLevel=LogLevel.None;o.Rules.Add(new(null,"GnazzosWorld.Api.Operations",LogLevel.Information,null));o.Rules.Add(new(null,"GnazzosWorld.Api.Security.ReadSecurityAudit",LogLevel.Information,null));});
        builder.Services.AddRateLimiter(o=>o.AddFixedWindowLimiter("operations-probe",l=>{l.PermitLimit=10;l.Window=TimeSpan.FromMinutes(1);l.QueueLimit=0;}));
    }
    public static void Middleware(WebApplication app)
    {
        if(!app.Configuration.GetValue<bool>("Operations:Enabled"))return;var telemetry=app.Services.GetRequiredService<OperationTelemetry>();app.Use(telemetry.Observe);
    }
    public static void Map(WebApplication app)
    {
        if(!app.Configuration.GetValue<bool>("Operations:Enabled"))return;var settings=app.Services.GetRequiredService<OperationsSettings>();
        app.MapGet("/health/live",(HttpContext c)=>{c.Response.Headers.CacheControl="no-store";return Results.Ok(new{status="live"});});
        app.MapGet("/health/ready",(HttpContext c)=>
        {
            c.Response.Headers.CacheControl="no-store";
            try
            {
                var state=CommerceMaintenance.ReadState(settings.Database);var path=Path.Combine(settings.ArtifactsRoot,"delivery_sample.wav");var file=new FileInfo(path);
                if(state.RestorePending||!file.Exists||file.Length!=PrivateDeliveryFixture.Length||(file.Attributes&FileAttributes.ReparsePoint)!=0||!ArtifactMatches(path))return Results.Json(new{status="not_ready"},statusCode:503);
                return Results.Ok(new{status="ready"});
            }
            catch(Exception ex) when(ex is IOException or InvalidDataException or UnauthorizedAccessException or Microsoft.Data.Sqlite.SqliteException){return Results.Json(new{status="not_ready"},statusCode:503);}
        });
        if(settings.FailureProbe)app.MapPost("/ops/lab-failure",async(HttpContext c)=>
        {
            var buffer=new byte[1025];var count=0;while(count<buffer.Length){var n=await c.Request.Body.ReadAsync(buffer.AsMemory(count),c.RequestAborted);if(n==0)break;count+=n;}
            if(count>1024)return Results.Problem(statusCode:400,title:"Probe body exceeds its lab bound.");
            throw new SyntheticOperationException();
        }).RequireRateLimiting("operations-probe");
    }
    private static bool ArtifactMatches(string path)
    {
        using var input=File.OpenRead(path);var bytes=new byte[checked((int)PrivateDeliveryFixture.Length+1)];var count=0;
        while(count<bytes.Length){var read=input.Read(bytes.AsSpan(count));if(read==0)break;count+=read;}
        return count==PrivateDeliveryFixture.Length&&Convert.ToHexString(SHA256.HashData(bytes.AsSpan(0,count))).ToLowerInvariant()==PrivateDeliveryFixture.Sha256;
    }
    public static Task Failure(HttpContext c)
    {
        var extensions=c.Items.TryGetValue("gnazzo.operation",out var operation)?new Dictionary<string,object?>{{"operationId",operation}}:null;
        return Results.Problem(statusCode:c.Request.Path.StartsWithSegments("/ops/lab-failure")?503:500,title:"The teaching service could not complete the request.",extensions:extensions).ExecuteAsync(c);
    }
    private sealed class SyntheticOperationException:Exception
    {public SyntheticOperationException():base("SYNTHETIC_EXCEPTION_PRIVATE_CANARY"){} }
}

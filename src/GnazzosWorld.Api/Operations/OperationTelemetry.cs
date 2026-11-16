using System.Diagnostics;
using System.Diagnostics.Metrics;
using GnazzosWorld.Core.Commerce;

namespace GnazzosWorld.Api.Operations;

public sealed class OperationTelemetry:IDisposable
{
    public const string SourceName="GnazzosWorld.Api.Operations";
    private readonly ActivitySource source=new(SourceName,"1.0");private readonly Meter meter=new(SourceName,"1.0");private readonly ActivityListener listener;
    private readonly Counter<long> completed;private readonly Histogram<double> duration;private readonly ILogger<OperationTelemetry> logger;
    public OperationTelemetry(OperationsSettings settings,ILogger<OperationTelemetry> logger)
    {
        this.logger=logger;completed=meter.CreateCounter<long>("gnazzo.operations.completed");duration=meter.CreateHistogram<double>("gnazzo.operations.duration","ms");
        listener=new ActivityListener{ShouldListenTo=s=>s.Name==SourceName,Sample=(ref ActivityCreationOptions<ActivityContext> o)=>
            o.Name=="lab.failure"||settings.TracePercent==100||(settings.TracePercent>0&&Convert.ToUInt32(o.TraceId.ToHexString()[..8],16)/(double)uint.MaxValue<settings.TracePercent/100d)?ActivitySamplingResult.AllDataAndRecorded:ActivitySamplingResult.None,
            ActivityStopped=a=>
            {
                // Export an allowlisted projection only; never serialize Activity, baggage, inherited tags or scopes.
                var operation=a.GetTagItem("gnazzo.operation") as string;var area=a.GetTagItem("gnazzo.area") as string;var outcome=a.GetTagItem("gnazzo.outcome") as string;
                if(!OrderContract.Id(operation)||!Areas.Contains(area)||!Outcomes.Contains(outcome))return;
                logger.LogInformation(new EventId(3502,"OperationTrace"),"Trace {OperationId} {TraceId} {Area} {Outcome}",operation,a.TraceId.ToHexString(),area,outcome);
            }};ActivitySource.AddActivityListener(listener);
    }
    private static readonly HashSet<string?> Areas=["health","catalog","owned","webhook","failure_probe","other"];
    private static readonly HashSet<string?> Outcomes=["ok","client_failure","server_failure","other"];
    public async Task Observe(HttpContext c,RequestDelegate next)
    {
        var operation=Guid.NewGuid().ToString("N");c.Items["gnazzo.operation"]=operation;c.Response.OnStarting(()=>{c.Response.Headers["X-Operation-Id"]=operation;return Task.CompletedTask;});
        var area=c.Request.Path.StartsWithSegments("/ops/lab-failure")?"failure_probe":c.Request.Path.StartsWithSegments("/health")?"health":c.Request.Path.StartsWithSegments("/api/v1/catalog")?"catalog":c.Request.Path.StartsWithSegments("/api/v1/commerce/test-payments/webhook")?"webhook":c.Request.Path.StartsWithSegments("/api/v1/commerce")||c.Request.Path.StartsWithSegments("/api/v1/requests")?"owned":"other";
        using var span=source.StartActivity(area=="failure_probe"?"lab.failure":"api.operation",ActivityKind.Internal);
        span?.SetTag("gnazzo.operation",operation);span?.SetTag("gnazzo.area",area);var started=Stopwatch.GetTimestamp();
        try{await next(c);}
        finally
        {
            var status=c.Response.StatusCode;var outcome=status>=500?"server_failure":status>=400?"client_failure":status>=200&&status<400?"ok":"other";var ms=Stopwatch.GetElapsedTime(started).TotalMilliseconds;
            span?.SetTag("gnazzo.outcome",outcome);if(status>=500)span?.SetStatus(ActivityStatusCode.Error);
            var tags=new TagList{{"area",area},{"outcome",outcome}};completed.Add(1,tags);duration.Record(ms,tags);
            var bucket=ms<10?"under_10ms":ms<100?"under_100ms":ms<1000?"under_1s":"over_1s";
            logger.LogInformation(new EventId(3501,"OperationCompleted"),"Operation {OperationId} {Area} {Outcome} {StatusClass} {DurationBucket}",operation,area,outcome,status/100,bucket);
        }
    }
    public void Dispose(){listener.Dispose();source.Dispose();meter.Dispose();}
}

using System.Collections.Concurrent;
using System.Diagnostics;
using System.Diagnostics.Metrics;
using System.Net;
using System.Text;
using System.Text.Json;
using GnazzosWorld.Api.Operations;
using GnazzosWorld.Core.Operations;
using Microsoft.Extensions.Logging;
using Xunit;

namespace GnazzosWorld.Api.Tests;

public sealed class OperationsApiTests:IDisposable
{
    private static CancellationToken Ct=>TestContext.Current.CancellationToken;
    private readonly string root=Path.Combine(Path.GetTempPath(),"gnazzo-operations-api-"+Guid.NewGuid().ToString("N"));private string Db=>Path.Combine(root,"commerce.sqlite3");private string Artifacts=>Path.Combine(root,"artifacts");
    private Dictionary<string,string?> Settings(bool probe=true)
    {Directory.CreateDirectory(Artifacts);if(!File.Exists(Db))CommerceMaintenance.Initialize(Db);File.Copy(Path.GetFullPath(Path.Combine(AppContext.BaseDirectory,"../../../../../src/GnazzosWorld.Api/Fixtures/delivery_sample.wav")),Path.Combine(Artifacts,"delivery_sample.wav"),true);return new(){{"Operations:Enabled","true"},{"Operations:FailureProbe",probe.ToString()},{"Operations:TracePercent","100"},{"Operations:ArtifactsRoot",Artifacts},{"Commerce:Database",Db}};}
    [Fact] public async Task Operations_endpoints_are_absent_by_default()
    {using var f=new RequestAuthorizationTests.Factory();using var c=f.Client();using var r=await c.GetAsync("/health/ready",Ct);Assert.Equal(HttpStatusCode.NotFound,r.StatusCode);}
    [Fact] public async Task Liveness_and_readiness_are_distinct_and_do_not_disclose_paths()
    {var settings=Settings();using var f=new RequestAuthorizationTests.Factory(operations:settings,environment:"Staging");using var c=f.Client();using var live=await c.GetAsync("/health/live",Ct);using var ready=await c.GetAsync("/health/ready",Ct);Assert.Equal(HttpStatusCode.OK,live.StatusCode);Assert.Equal(HttpStatusCode.OK,ready.StatusCode);File.Delete(Path.Combine(Artifacts,"delivery_sample.wav"));using var missing=await c.GetAsync("/health/ready",Ct);Assert.Equal(HttpStatusCode.ServiceUnavailable,missing.StatusCode);Assert.DoesNotContain(root,await missing.Content.ReadAsStringAsync(Ct));using var stillLive=await c.GetAsync("/health/live",Ct);Assert.Equal(HttpStatusCode.OK,stillLive.StatusCode);}
    [Fact] public async Task Restored_database_holds_readiness_until_explicit_generation_release()
    {var settings=Settings();CommerceMaintenance.Backup(Db,Path.Combine(root,"backup"));var state=CommerceMaintenance.Restore(Path.Combine(root,"backup"),Path.Combine(root,"restored"));var path=Path.Combine(root,"restored","commerce.sqlite3");settings["Commerce:Database"]=path;using var f=new RequestAuthorizationTests.Factory(operations:settings,environment:"Staging");using var c=f.Client();using var held=await c.GetAsync("/health/ready",Ct);Assert.Equal(HttpStatusCode.ServiceUnavailable,held.StatusCode);CommerceMaintenance.ReleaseRestoredForLab(path,state.Generation);using var ready=await c.GetAsync("/health/ready",Ct);Assert.Equal(HttpStatusCode.OK,ready.StatusCode);}
    [Fact] public async Task Failure_has_server_generated_correlation_and_allowlisted_log_trace_fields()
    {var settings=Settings();using var capture=new Capture();using var f=new RequestAuthorizationTests.Factory(capture:capture,operations:settings,environment:"Staging");using var c=f.Client();using var request=new HttpRequestMessage(HttpMethod.Post,"/ops/lab-failure?private=PRIVATE_QUERY_CANARY"){Content=new StringContent("PRIVATE_BODY_CANARY",Encoding.UTF8,"application/json")};request.Headers.Add("X-Correlation-Id","PRIVATE_HEADER_CANARY");request.Headers.Add("X-Delivery-Grant","PRIVATE_GRANT_CANARY");request.Headers.Authorization=new("Bearer","PRIVATE_TOKEN_CANARY");using var response=await c.SendAsync(request,Ct);Assert.Equal(HttpStatusCode.ServiceUnavailable,response.StatusCode);var id=Assert.Single(response.Headers.GetValues("X-Operation-Id"));Assert.True(GnazzosWorld.Core.Commerce.OrderContract.Id(id));var body=await response.Content.ReadAsStringAsync(Ct);Assert.Contains(id,body);var records=capture.Records.ToArray();Assert.Contains(records,x=>x.EventId==3501&&x.Fields.TryGetValue("OperationId",out var op)&&op==id);Assert.Contains(records,x=>x.EventId==3502&&x.Fields.TryGetValue("OperationId",out var op)&&op==id);var text=JsonSerializer.Serialize(records)+body;foreach(var canary in new[]{"PRIVATE_QUERY_CANARY","PRIVATE_BODY_CANARY","PRIVATE_HEADER_CANARY","PRIVATE_GRANT_CANARY","PRIVATE_TOKEN_CANARY","SYNTHETIC_EXCEPTION_PRIVATE_CANARY"})Assert.DoesNotContain(canary,text);Assert.DoesNotContain(records,x=>x.ExceptionPresent);}
    [Fact] public async Task Probe_can_be_disabled_or_reject_oversized_bodies()
    {using(var f=new RequestAuthorizationTests.Factory(operations:Settings(false),environment:"Staging"))using(var c=f.Client()){using var r=await c.PostAsync("/ops/lab-failure",null,Ct);Assert.Equal(HttpStatusCode.NotFound,r.StatusCode);}using var f2=new RequestAuthorizationTests.Factory(operations:Settings(),environment:"Staging");using var c2=f2.Client();using var large=await c2.PostAsync("/ops/lab-failure",new StringContent(new string('x',1025)),Ct);Assert.Equal(HttpStatusCode.BadRequest,large.StatusCode);}
    [Fact] public async Task Custom_metrics_use_only_bounded_area_and_outcome_tags()
    {var observed=new ConcurrentQueue<string[]>();using var listener=new MeterListener{InstrumentPublished=(instrument,l)=>{if(instrument.Meter.Name==OperationTelemetry.SourceName)l.EnableMeasurementEvents(instrument);}};listener.SetMeasurementEventCallback<long>((_,_,tags,_)=>observed.Enqueue(tags.ToArray().Select(t=>t.Key).ToArray()));listener.Start();using var f=new RequestAuthorizationTests.Factory(operations:Settings(),environment:"Staging");using var c=f.Client();using var r=await c.GetAsync("/health/live?private=PRIVATE_METRIC_CANARY",Ct);Assert.Equal(HttpStatusCode.OK,r.StatusCode);Assert.Contains(observed,x=>x.SequenceEqual(new[]{"area","outcome"}));}
    [Theory][InlineData("Production",false)][InlineData("Staging",true)]
    public void Operations_profile_rejects_production_or_staging_synthetic_write_flags(string environment,bool writes)
    {var settings=Settings();settings["Commerce:Enabled"]=writes.ToString();using var f=new RequestAuthorizationTests.Factory(operations:settings,environment:environment);Assert.ThrowsAny<Exception>(()=>f.Client());}
    private sealed record LogRecord(int EventId,string Category,IReadOnlyDictionary<string,string> Fields,bool ExceptionPresent);
    private sealed class Capture:ILoggerProvider
    {
        public ConcurrentQueue<LogRecord> Records{get;}=new();public ILogger CreateLogger(string category)=>new Logger(category,Records);public void Dispose(){}
        private sealed class Logger(string category,ConcurrentQueue<LogRecord> records):ILogger
        {public IDisposable? BeginScope<TState>(TState state) where TState:notnull=>null;public bool IsEnabled(LogLevel level)=>true;public void Log<TState>(LogLevel level,EventId id,TState state,Exception? exception,Func<TState,Exception?,string> formatter){var fields=state is IEnumerable<KeyValuePair<string,object?>> pairs?pairs.ToDictionary(p=>p.Key,p=>p.Value?.ToString()??""):new Dictionary<string,string>();records.Enqueue(new(id.Id,category,fields,exception is not null));}}
    }
    public void Dispose(){if(Directory.Exists(root))Directory.Delete(root,true);}
}

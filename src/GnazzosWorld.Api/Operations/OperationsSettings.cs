namespace GnazzosWorld.Api.Operations;

public sealed record OperationsSettings(string Database,string ArtifactsRoot,int TracePercent,bool FailureProbe)
{
    public static OperationsSettings Read(IConfiguration c,IHostEnvironment env)
    {
        if(!env.IsDevelopment()&&!env.IsStaging())throw new InvalidOperationException("Operations lab supports Development or isolated Staging only.");
        if(!env.IsDevelopment()&&new[]{"Commerce:Enabled","Payments:Enabled","Fulfillment:Enabled"}.Any(k=>c.GetValue<bool>(k)))throw new InvalidOperationException("Staging lab cannot enable synthetic business-write routes.");
        var db=c["Commerce:Database"]??"";var artifacts=c["Operations:ArtifactsRoot"]??"";var percent=c.GetValue<int?>("Operations:TracePercent")??10;
        if(!Path.IsPathFullyQualified(db)||!Path.IsPathFullyQualified(artifacts)||percent is <0 or >100)throw new InvalidOperationException("Configure absolute lab storage paths and a bounded trace percentage.");
        return new(db,artifacts,percent,c.GetValue<bool>("Operations:FailureProbe"));
    }
}

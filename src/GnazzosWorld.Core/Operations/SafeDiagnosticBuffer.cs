namespace GnazzosWorld.Core.Operations;

public enum DiagnosticArea { Network, Storage, Media, Identity }
public enum DiagnosticFault { Unavailable, Rejected, Interrupted, Unexpected }
public sealed record SafeDiagnosticEvent(string Id,DiagnosticArea Area,DiagnosticFault Fault,DateTimeOffset At);
/// <summary>Local consent-controlled projection, not an SDK crash dump or remote exporter. No free text/exception fields.</summary>
public sealed class SafeDiagnosticBuffer(TimeProvider clock,bool enabled=false)
{
    private readonly object sync=new();private readonly Queue<SafeDiagnosticEvent> events=new();private bool consent=enabled;
    public bool Enabled{get{lock(sync)return consent;}}
    public void SetConsent(bool value){lock(sync){consent=value;if(!value)events.Clear();}}
    public bool Record(DiagnosticArea area,DiagnosticFault fault)
    {
        if(!Enum.IsDefined(area)||!Enum.IsDefined(fault))throw new ArgumentOutOfRangeException(nameof(area));
        lock(sync){if(!consent)return false;while(events.Count>=20)events.Dequeue();events.Enqueue(new(Guid.NewGuid().ToString("N"),area,fault,clock.GetUtcNow()));return true;}
    }
    public IReadOnlyList<SafeDiagnosticEvent> Snapshot(){lock(sync)return events.ToArray();}
}

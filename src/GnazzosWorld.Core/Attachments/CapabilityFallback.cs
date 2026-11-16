namespace GnazzosWorld.Core.Attachments;

public static class CapabilityFallback
{
    public static bool RequestOptionalHaptic(bool enabled, Action request)
    {
        if (!enabled) return false;
        try { request(); return true; } catch { return false; }
    }
    public static string NetworkHint(bool reportedInternet) => reportedInternet
        ? "The device reports internet access; service reachability is untested. Local staging still works offline."
        : "Internet access is not reported. Local attachment staging remains available.";
}

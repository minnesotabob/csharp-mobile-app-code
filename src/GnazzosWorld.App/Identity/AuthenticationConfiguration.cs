using GnazzosWorld.Core.Identity;

namespace GnazzosWorld.App.Identity;

public static class AuthenticationConfiguration
{
    // Replace null with new("your-tenant.us.auth0.com", "your-public-client-id")
    // after registering a Native application and the exact callback in the lab.
    // Never add a client secret. Null keeps the distributable app unconfigured.
    public static NativeIdentitySettings? Current { get; } = null;
    // Separate opt-in: use the same identity plus your API identifier and HTTPS origin.
    public static ApiSessionSettings? ApiSession { get; } = null;
    public static bool EnableOrderLabScopes { get; } = false; // Opt in only after registering/granting orders:read and orders:write.
}

using GnazzosWorld.Core.Identity;

namespace GnazzosWorld.App.Identity;

public static class AuthenticationConfiguration
{
    // Replace null with new("your-tenant.us.auth0.com", "your-public-client-id")
    // after registering a Native application and the exact callback in the lab.
    // Never add a client secret. Null keeps the distributable app unconfigured.
    public static NativeIdentitySettings? Current { get; } = null;
}

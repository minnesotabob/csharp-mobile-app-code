using Android.App;
using Android.Content;
using Android.Content.PM;
using GnazzosWorld.Core.Identity;

namespace GnazzosWorld.App;

[Activity(NoHistory = true, LaunchMode = LaunchMode.SingleTop, Exported = true)]
[IntentFilter([Intent.ActionView], Categories = [Intent.CategoryDefault, Intent.CategoryBrowsable],
    DataScheme = NativeIdentitySettings.CallbackScheme, DataHost = "callback", DataPath = "/")]
public sealed class AuthenticationCallbackActivity : Microsoft.Maui.Authentication.WebAuthenticatorCallbackActivity { }

#if GNAZZO_ANDROID_RELEASE_LAB && ANDROID
using Android.App;
using Android.Content.PM;
using Android.OS;
using Android.Widget;
using GnazzosWorld.App.Storage;
using GnazzosWorld.Core.Orders;

namespace GnazzosWorld.App;

// Exported only in the isolated testOnly lab build, never in ordinary app builds.
[Activity(Name = ReleaseProbeActivity.Component, Exported = true, Label = "Disposable release probe", ExcludeFromRecents = true)]
public sealed class ReleaseProbeActivity : Activity
{
    public const string Package = "com.johngnazzo.gnazzosworld.releaselab";
    public const string Component = Package + ".ReleaseProbeActivity";
    protected override void OnCreate(Bundle? savedInstanceState)
    {
        base.OnCreate(savedInstanceState);
        var outcome = "failed";
        try
        {
            if (PackageName != Package || (ApplicationInfo!.Flags & ApplicationInfoFlags.TestOnly) == 0)
                throw new InvalidOperationException("Probe is restricted to the disposable test package.");
            var expected = new SongRequest { Genre = "Synthetic release folk", Occasion = "Invented upgrade", Story = "Synthetic draft surviving an Android package update", ContactName = "Release Lab", Email = "release@example.test" };
            var store = new FileRequestDraftStore();
            switch (Intent?.GetStringExtra("probe"))
            {
                case "seed": store.Save(expected); outcome = store.Load() == expected ? "seeded" : "failed"; break;
                case "verify": outcome = store.Load() == expected ? "verified" : "failed"; break;
            }
        }
        catch { outcome = "failed"; }
        var label = new TextView(this) { Text = "Disposable release probe: " + outcome, ContentDescription = "release-probe-" + outcome };
        label.TextSize = 22; SetContentView(label);
    }
}
#endif

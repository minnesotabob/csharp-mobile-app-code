using System.Globalization;
using System.Resources;

namespace GnazzosWorld.Core.Localization;

public static class UiText
{
    private static readonly ResourceManager Resources = new(
        "GnazzosWorld.Core.Localization.Strings", typeof(UiText).Assembly);
    public static string Get(string key, CultureInfo? culture = null)
        => Resources.GetString(key, culture) ?? key;
    public static string Home => Get(nameof(Home));
    public static string Discover => Get(nameof(Discover));
    public static string Welcome => Get(nameof(Welcome));
    public static string Intro => Get(nameof(Intro));
    public static string Browse => Get(nameof(Browse));
    public static string SearchHint => Get(nameof(SearchHint));
    public static string Filter => Get(nameof(Filter));
    public static string All => Get(nameof(All));
    public static string Books => Get(nameof(Books));
    public static string Songs => Get(nameof(Songs));
    public static string Podcasts => Get(nameof(Podcasts));
    public static string OpenSelected => Get(nameof(OpenSelected));
    public static string Empty => Get(nameof(Empty));
    public static string Choose => Get(nameof(Choose));
    public static string NotFound => Get(nameof(NotFound));
    public static string Details => Get(nameof(Details));
    public static string ResultsFormat => Get(nameof(ResultsFormat));
    public static string NavigationError => Get(nameof(NavigationError));
    public static string Ok => Get(nameof(Ok));
    public static string Error => Get(nameof(Error));
}

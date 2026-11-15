using GnazzosWorld.Core.Catalog;
using GnazzosWorld.Core.Localization;
using GnazzosWorld.Core.Presentation;
using System.Globalization;
using Xunit;

namespace GnazzosWorld.Core.Tests;

public sealed class PresentationTests
{
    private sealed class Navigation : ICatalogNavigation
    {
        public List<string> Opened { get; } = [];
        public Task OpenItemAsync(string id) { Opened.Add(id); return Task.CompletedTask; }
    }

    [Fact]
    public void Query_and_filter_update_results_and_clear_hidden_selection()
    {
        var vm = new CatalogBrowserViewModel(CatalogLoader.LoadBundled(), new Navigation());
        vm.SelectedItem = vm.Items.First(x => x.Kind == ContentKind.Book);
        vm.FilterIndex = 2; // Songs
        Assert.Equal(2, vm.Items.Count);
        Assert.Null(vm.SelectedItem);
        Assert.False(vm.OpenDetails.CanExecute(null));
        vm.Query = "BERET";
        Assert.Equal("Béret violet", Assert.Single(vm.Items).Title);
        Assert.False(vm.Detail.HasItem);
    }

    [Fact]
    public void Selection_survives_a_query_that_still_matches()
    {
        var vm = new CatalogBrowserViewModel(CatalogLoader.LoadBundled(), new Navigation());
        var item = vm.Items.First(x => x.Title == "Kai the Ninja");
        vm.SelectedItem = item;
        vm.Query = "kai";
        Assert.Equal(item.Id, vm.SelectedItem!.Id);
        Assert.Equal(item.Title, vm.Detail.Title);
    }

    [Fact]
    public async Task Command_opens_the_selected_stable_id()
    {
        var nav = new Navigation();
        var vm = new CatalogBrowserViewModel(CatalogLoader.LoadBundled(), nav);
        await vm.OpenDetails.ExecuteAsync();
        Assert.Empty(nav.Opened);
        vm.SelectedItem = vm.Items[0];
        await vm.OpenDetails.ExecuteAsync();
        Assert.Equal(vm.SelectedItem.Id, Assert.Single(nav.Opened));
    }

    [Fact]
    public async Task Command_prevents_overlap_and_reenables_after_completion()
    {
        var completion = new TaskCompletionSource(
            TaskCreationOptions.RunContinuationsAsynchronously);
        var calls = 0;
        var command = new AsyncCommand(() => { calls++; return completion.Task; },
            () => true, _ => { });
        var first = command.ExecuteAsync();
        Assert.False(command.CanExecute(null));
        await command.ExecuteAsync();
        Assert.Equal(1, calls);
        completion.SetResult();
        await first;
        Assert.True(command.CanExecute(null));
    }

    [Fact]
    public async Task Command_reports_failure_and_recovers()
    {
        Exception? observed = null;
        var command = new AsyncCommand(
            () => Task.FromException(new InvalidOperationException("demo")),
            () => true, e => observed = e);
        await command.ExecuteAsync();
        Assert.IsType<InvalidOperationException>(observed);
        Assert.True(command.CanExecute(null));
    }

    [Fact]
    public void Notifications_include_derived_results()
    {
        var vm = new CatalogBrowserViewModel(CatalogLoader.LoadBundled(), new Navigation());
        var names = new List<string?>();
        vm.PropertyChanged += (_, e) => names.Add(e.PropertyName);
        vm.Query = "no match";
        Assert.Contains(nameof(vm.Items), names);
        Assert.Contains(nameof(vm.ResultSummary), names);
        Assert.Empty(vm.Items);
    }

    [Theory]
    [InlineData(-2)] [InlineData(4)]
    public void Invalid_filter_is_rejected(int index)
    {
        var vm = new CatalogBrowserViewModel(CatalogLoader.LoadBundled(), new Navigation());
        Assert.Throws<ArgumentOutOfRangeException>(() => vm.FilterIndex = index);
    }

    [Fact]
    public void Unselected_picker_state_normalizes_to_all()
    {
        var vm = new CatalogBrowserViewModel(CatalogLoader.LoadBundled(), new Navigation());
        vm.FilterIndex = 1;
        vm.FilterIndex = -1;
        Assert.Equal(0, vm.FilterIndex);
        Assert.Equal(5, vm.Items.Count);
    }

    [Fact]
    public void Unknown_detail_clears_previous_content()
    {
        var vm = new DetailViewModel(CatalogLoader.LoadBundled());
        vm.Load("book-kai-01");
        Assert.True(vm.HasItem);
        vm.Load("does-not-exist");
        Assert.False(vm.HasItem);
        Assert.Equal("", vm.Creator);
        Assert.NotEmpty(vm.Message);
    }

    [Theory]
    [InlineData("")] [InlineData("../private")] [InlineData("abc/def")]
    [InlineData(" id ")] [InlineData("id?next=elsewhere")]
    public void Invalid_route_id_is_rejected(string id)
        => Assert.Null(RoutePayload.ReadItemId(new Dictionary<string, object>
            { ["id"] = id }));

    [Fact]
    public void Route_accepts_known_id_and_rejects_wrong_type_or_missing_key()
    {
        Assert.Equal("book-kai-01", RoutePayload.ReadItemId(
            new Dictionary<string, object> { ["id"] = "book-kai-01" }));
        Assert.Null(RoutePayload.ReadItemId(new Dictionary<string, object>()));
        Assert.Null(RoutePayload.ReadItemId(
            new Dictionary<string, object> { ["id"] = 42 }));
        Assert.False(RoutePayload.IsValidId(new string('a', 129)));
    }

    [Theory]
    [InlineData(899.9, false)] [InlineData(900, true)]
    [InlineData(1200, true)] [InlineData(-1, false)]
    public void Adaptive_layout_uses_available_width(double width, bool wide)
        => Assert.Equal(wide, AdaptiveLayout.IsWide(width));

    [Fact]
    public void Nonfinite_width_is_never_wide()
    {
        Assert.False(AdaptiveLayout.IsWide(double.NaN));
        Assert.False(AdaptiveLayout.IsWide(double.PositiveInfinity));
    }

    [Fact]
    public void Spanish_resources_and_neutral_fallback_are_available()
    {
        Assert.Equal("Explorar", UiText.Get("Discover", CultureInfo.GetCultureInfo("es")));
        Assert.Equal("Discover", UiText.Get("Discover", CultureInfo.GetCultureInfo("fr")));
    }

    [Theory]
    [InlineData("en", "1 result")]
    [InlineData("es", "1 resultado")]
    public void One_match_has_a_localized_singular_summary(string culture, string expected)
    {
        var previous = CultureInfo.CurrentUICulture;
        try
        {
            CultureInfo.CurrentUICulture = CultureInfo.GetCultureInfo(culture);
            var vm = new CatalogBrowserViewModel(CatalogLoader.LoadBundled(), new Navigation());
            vm.Query = "Kai";
            Assert.Equal(expected, vm.ResultSummary);
        }
        finally { CultureInfo.CurrentUICulture = previous; }
    }
}

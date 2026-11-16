using GnazzosWorld.Core.Catalog;
using GnazzosWorld.Core.Performance;
using GnazzosWorld.Core.Presentation;

namespace GnazzosWorld.App.Pages;

// Deliberately separate public stress fixture; no network, customer data or benchmark claim.
public sealed class PerformanceLabPage : ContentPage
{
    private readonly CollectionView list = new() { SelectionMode = SelectionMode.None, AutomationId = "PerformanceList" };
    private readonly Label status = new() { Text = "Load the synthetic fixture explicitly. Native measurements are not recorded by CI." };
    private readonly SearchBar query = new() { Placeholder = "Search synthetic metadata", AutomationId = "PerformanceQuery" };
    private readonly Picker mode = new() { Title = "Image size fixture", ItemsSource = new[] { "Thumbnail 96 x 112", "Full image 1200 x 1400" }, SelectedIndex = 0, AutomationId = "PerformanceImageMode" };
    private readonly DebouncedQuery input = new();
    private CatalogSearchSnapshot? snapshot;
    private CancellationTokenSource? pending;
    private long generation;
    private int templates;
    private bool visible;

    public PerformanceLabPage()
    {
        Title = "Catalog performance lab";
        var load = new Button { Text = "Load 10,000 synthetic rows", AutomationId = "PerformanceLoad" };
        var inspect = new Button { Text = "Inspect template constructions", AutomationId = "PerformanceInspect" };
        var intro = new Label { Text = "Compare the two bundled size variants on the same named Release candidate/device. The list has a finite Grid viewport and variable-height text. Template constructions are not live-cell count, frame time, decoded-memory size or an accessibility pass." };
        var controls = new VerticalStackLayout { Spacing = 8, Children = { intro, load, mode, query, inspect, status } };
        var grid = new Grid { Padding = 16, RowDefinitions = { new(GridLength.Auto), new(GridLength.Star) }, RowSpacing = 8 };
        var panel = new ScrollView { Content = controls, MaximumHeightRequest = 330 };
        grid.Add(panel); grid.Add(list, 0, 1); Content = grid;
        SemanticProperties.SetDescription(query, "Search generated public performance records");
        SemanticProperties.SetDescription(mode, "Choose thumbnail or full-resolution image fixture");
        load.Clicked += async (_, _) => await LoadAsync();
        mode.SelectedIndexChanged += async (_, _) => { if (snapshot is not null && visible) await LoadAsync(); };
        inspect.Clicked += (_, _) => status.Text = $"{templates} template constructions since last load; {list.ItemsSource?.Cast<object>().Count() ?? 0} current result rows. This does not certify virtualization.";
        query.TextChanged += async (_, e) => await input.UpdateAsync(e.NewTextValue, text => _ = SearchAsync(text), () => status.Text = "Search is unconfirmed; reload explicitly.");
    }
    protected override void OnAppearing() { base.OnAppearing(); visible = true; }
    protected override void OnDisappearing()
    {
        visible = false; ++generation; input.Cancel(); pending?.Cancel();
        list.ItemsSource = null; list.ItemTemplate = null; snapshot = null;
        status.Text = "Stress view released. Load again to continue.";
        base.OnDisappearing();
    }
    private async Task LoadAsync()
    {
        if (!visible) return;
        var own = ++generation; input.Cancel(); pending?.Cancel();
        snapshot = null; list.ItemsSource = null; list.ItemTemplate = null;
        using var work = new CancellationTokenSource(); pending = work;
        var imageName = mode.SelectedIndex == 1 ? "perf_large.png" : "perf_thumb.png";
        try
        {
            status.Text = "Preparing synthetic metadata and encoded image fixture.";
            var data = await Task.Run(() => new CatalogSearchSnapshot(PerformanceCatalogFixture.Create()), work.Token);
            await using var stream = await FileSystem.Current.OpenAppPackageFileAsync(imageName);
            using var encoded = new MemoryStream(); var buffer = new byte[4096]; int read;
            while ((read = await stream.ReadAsync(buffer, work.Token)) > 0)
            { if (encoded.Length + read > 2 * 1024 * 1024) throw new InvalidDataException(); encoded.Write(buffer, 0, read); }
            var bytes = encoded.ToArray();
            if (!visible || own != generation || work.IsCancellationRequested) return;
            snapshot = data; templates = 0;
            list.ItemsSource = null;
            list.ItemTemplate = new DataTemplate(() => { templates++; return new StressCard(bytes); });
            status.Text = $"{data.Items.Count} synthetic records prepared with {imageName}. Scroll, rotate and inspect large text on the named device.";
            await SearchAsync(query.Text ?? "");
        }
        catch (OperationCanceledException) when (work.IsCancellationRequested) { }
        catch { if (visible && own == generation) status.Text = "Fixture load unconfirmed. Choose Load to retry."; }
        finally { if (ReferenceEquals(pending, work)) pending = null; }
    }
    private async Task SearchAsync(string text)
    {
        var data = snapshot; if (!visible || data is null) return;
        var own = ++generation;
        try
        {
            var result = await Task.Run(() => data.Find(text));
            if (visible && own == generation && ReferenceEquals(snapshot, data))
            { list.ItemsSource = result; status.Text = $"{result.Count} synthetic matches. Inspect template count separately."; }
        }
        catch { if (visible && own == generation) status.Text = "Search unconfirmed. Reload explicitly."; }
    }
    private sealed class StressCard : Grid
    {
        private readonly Label title = new(); private readonly Label creator = new();
        public StressCard(byte[] encoded)
        {
            Padding = 10; ColumnDefinitions.Add(new(60)); ColumnDefinitions.Add(new(GridLength.Star)); ColumnSpacing = 10;
            var picture = new Image { WidthRequest = 48, HeightRequest = 56, Aspect = Aspect.AspectFit,
                Source = ImageSource.FromStream(() => new MemoryStream(encoded, writable: false)) };
            AutomationProperties.SetIsInAccessibleTree(picture, false);
            this.Add(picture); this.Add(new VerticalStackLayout { Spacing = 4, Children = { title, creator } }, 1);
        }
        protected override void OnBindingContextChanged()
        {
            base.OnBindingContextChanged(); var item = BindingContext as CatalogItem;
            title.Text = item?.Title ?? ""; creator.Text = item?.Creator ?? "";
        }
    }
}

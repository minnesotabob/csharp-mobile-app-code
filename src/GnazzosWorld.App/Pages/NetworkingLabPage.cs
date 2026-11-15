using GnazzosWorld.Core.Networking;

namespace GnazzosWorld.App.Pages;

public sealed class NetworkingLabPage : ContentPage
{
    private readonly DemoCatalogSource source;
    private readonly Label hint = new();
    private readonly Label result = new() { Text = "The main catalog still uses bundled data." };
    private readonly Button load = new() { Text = "Run demo catalog GET" };
    private CancellationTokenSource? pending;
    private int generation;

    public NetworkingLabPage(DemoCatalogSource source)
    {
        this.source = source; Title = "Networking lab";
        Content = new ScrollView { Content = new VerticalStackLayout
        {
            Padding = 24, Spacing = 16, Children =
            {
                new Label { Text = "HTTP contract demo", FontSize = 26 },
                new Label { Text = "This demo reads through an in-process HTTP handler. It does not contact a server or test your internet connection." },
                hint, load, result
            }
        }};
        load.Clicked += OnLoad;
    }
    protected override void OnAppearing()
    {
        base.OnAppearing();
        hint.Text = $"Connectivity hint: {Connectivity.Current.NetworkAccess}. This does not verify service reachability.";
    }
    protected override void OnDisappearing()
    {
        generation++; pending?.Cancel();
        base.OnDisappearing();
    }
    private async void OnLoad(object? sender, EventArgs e)
    {
        if (pending is not null) return;
        using var cts = new CancellationTokenSource(); pending = cts;
        var owner = ++generation; load.IsEnabled = false; result.Text = "Loading demo response...";
        try
        {
            var items = await source.LoadAsync(cts.Token);
            if (owner == generation) result.Text = $"Validated {items.Count} demo records. Main catalog unchanged.";
        }
        catch (OperationCanceledException)
        { if (owner == generation) result.Text = "Demo load canceled."; }
        catch (CatalogFetchException ex)
        { if (owner == generation) result.Text = $"Demo load failed: {ex.Problem}. Try again."; }
        finally
        {
            pending = null; load.IsEnabled = true;
            if (owner != generation) result.Text = "Demo load stopped when you left. Run it again.";
        }
    }
}

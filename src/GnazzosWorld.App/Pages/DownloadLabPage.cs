using GnazzosWorld.Core.Downloads;

namespace GnazzosWorld.App.Pages;

/// <summary>Foreground, explicit public-fixture transfer. No private deliveries or background job registration.</summary>
public sealed class DownloadLabPage : ContentPage
{
    private readonly PublicMediaDownloader downloads;
    private readonly Entry origin = new() { Text = "https://localhost:7443", Placeholder = "Your trusted HTTPS API origin", Keyboard = Keyboard.Url };
    private readonly Label status = new() { Text = "Configure your trusted development HTTPS origin, then download. Device localhost refers to the device; no certificate validation bypass is supplied." };
    private readonly ProgressBar progress = new();
    private readonly Button start = new() { Text = "Download / resume public timing fixture" };
    private readonly Button cancel = new() { Text = "Cancel; retain partial", IsEnabled = false };
    private readonly Button inspect = new() { Text = "Verify stored fixture" };
    private readonly Button delete = new() { Text = "Delete fixture and partial" };
    private CancellationTokenSource? pending;
    private bool visible;
    private Window? observedWindow;
    public DownloadLabPage(PublicMediaDownloader downloads)
    {
        this.downloads = downloads; Title = "Public download lab";
        Content = new ScrollView { Content = new VerticalStackLayout { Padding = 24, Spacing = 12, Children =
        {
            new Label { Text = "Downloads and media storage", FontSize = 25 },
            new Label { Text = "Original 10-second WAV only. Public cache, explicit foreground transfer; no account credentials, private deliveries, autoplay or background execution. Cancel retains a partial; availability requires complete length and SHA-256 verification. The OS may clear this cache." },
            origin, start, cancel, progress, status, inspect, delete
        }}};
        start.Clicked += async (_, _) => await RunAsync(async (asset, ct) =>
        {
            var active = pending;
            await downloads.DownloadAsync(asset, p => Dispatcher.Dispatch(() =>
            {
                if (!visible || !ReferenceEquals(pending, active)) return;
                progress.Progress = (double)p.Received / p.Total;
                status.Text = $"{p.Received} / {p.Total} bytes received; integrity verification still required.";
            }), ct);
            status.Text = "Complete public fixture verified and stored. No automatic playback."; progress.Progress = 1;
        });
        cancel.Clicked += (_, _) => pending?.Cancel();
        inspect.Clicked += async (_, _) => await RunAsync(async (asset, ct) =>
            status.Text = await downloads.GetVerifiedAsync(asset, ct) is null ? "No verified fixture is currently available." : "Stored fixture length and hash verified again.");
        delete.Clicked += async (_, _) => await RunAsync(async (asset, ct) =>
        {
            await downloads.DeleteAsync(asset, ct); progress.Progress = 0; status.Text = "Public fixture and partial removed.";
        });
    }
    protected override void OnAppearing()
    {
        base.OnAppearing(); visible = true; observedWindow = Window;
        if (observedWindow is not null) observedWindow.Stopped += OnStopped;
    }
    protected override void OnDisappearing()
    {
        visible = false; pending?.Cancel();
        if (observedWindow is not null) observedWindow.Stopped -= OnStopped;
        observedWindow = null; base.OnDisappearing();
    }
    private void OnStopped(object? sender, EventArgs e) => pending?.Cancel();
    private async Task RunAsync(Func<PublicMediaAsset, CancellationToken, Task> action)
    {
        if (pending is not null || !visible) return;
        using var cancellation = new CancellationTokenSource(); pending = cancellation;
        start.IsEnabled = inspect.IsEnabled = delete.IsEnabled = origin.IsEnabled = false; cancel.IsEnabled = true;
        try
        {
            var asset = PublicDownloadFixture.At(new Uri(origin.Text ?? ""));
            await action(asset, cancellation.Token);
        }
        catch (OperationCanceledException) { status.Text = "Transfer canceled or timed out. A partial may remain; retry explicitly to resume."; }
        catch { status.Text = "The action failed. Check trusted HTTPS, fixture headers, quota/free space and the lab worksheet. No new verified download is reported."; }
        finally
        {
            pending = null; start.IsEnabled = inspect.IsEnabled = delete.IsEnabled = origin.IsEnabled = true; cancel.IsEnabled = false;
        }
    }
}

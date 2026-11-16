using GnazzosWorld.App.Capabilities;
using GnazzosWorld.Core.Attachments;

namespace GnazzosWorld.App.Pages;

/// <summary>Transient local synthetic-reference scope. No customer upload or account attachment composition.</summary>
public sealed class CapabilityLabPage : ContentPage
{
    private readonly AttachmentWorkspace workspace;
    private readonly AttachmentExportStore exports;
    private readonly Label status = new() { Text = "Choose a synthetic reference. The lab stages locally and uploads nothing." };
    private readonly Label connectivity = new();
    private readonly Image preview = new() { HeightRequest = 200, Aspect = Aspect.AspectFit };
    private readonly Switch haptics = new();
    private readonly Button text = new() { Text = "Choose UTF-8 text reference" };
    private readonly Button photo = new() { Text = "Choose one reference photo" };
    private readonly Button share = new() { Text = "Share selected staged reference" };
    private CancellationTokenSource? pending;
    private Window? observedWindow;
    private Shell? observedShell;
    private bool picking, attached, departed, foreground, networkSubscribed;
    public CapabilityLabPage(AttachmentExportStore exports)
    {
        this.exports = exports; Title = "Native capability lab";
        workspace = new(Path.Combine(FileSystem.Current.CacheDirectory, "attachment-lab"), new NativeImageNormalizer());
        var cancel = new Button { Text = "Cancel preparation" };
        var clear = new Button { Text = "Clear this local attachment scope" };
        var clearExports = new Button { Text = "Remove local exported copies" };
        text.Clicked += async (_, _) => await RunAsync(ct => PickAsync(false, ct));
        photo.Clicked += async (_, _) => await RunAsync(ct => PickAsync(true, ct));
        cancel.Clicked += (_, _) => { pending?.Cancel(); status.Text = "Cancellation requested. Dismiss the system picker if it remains open."; };
        clear.Clicked += async (_, _) =>
        {
            pending?.Cancel(); preview.Source = null;
            try { await workspace.ClearAsync().WaitAsync(TimeSpan.FromSeconds(5)); status.Text = "Local attachment scope cleared."; }
            catch { status.Text = "Selection cleared from use; filesystem cleanup is unconfirmed and may still be finishing."; }
        };
        share.Clicked += async (_, _) => await RunAsync(async ct =>
        {
            var lease = workspace.Generation; var receipt = workspace.Current ?? throw new InvalidOperationException();
            var bytes = await workspace.ReadCurrentAsync(lease, ct);
            var path = await exports.CreateAsync(receipt, bytes, ct); ct.ThrowIfCancellationRequested();
            if (lease != workspace.Generation || departed) throw new OperationCanceledException(ct);
            await Share.Default.RequestAsync(new ShareFileRequest
            {
                Title = "Share the selected synthetic reference", File = new ShareFile(path, receipt.MediaType),
                PresentationSourceBounds = new Rect(Math.Max(1, Width / 2), Math.Max(1, Height / 2), 1, 1)
            });
            status.Text = "Share sheet returned; no recipient or transfer is confirmed. Local export remains until cleanup.";
        });
        clearExports.Clicked += async (_, _) => await RunAsync(async ct =>
        { await exports.ClearAsync(ct); status.Text = "Local exported copies removed. Copies retained by other apps cannot be revoked here."; });
        Content = new ScrollView { Content = new VerticalStackLayout { Padding = 24, Spacing = 12, Children =
        {
            new Label { Text = "Optional references and native fallbacks", FontSize = 25 },
            new Label { Text = "Use synthetic text/photos. One local reference at a time; no customer upload. Sharing exposes selected content to an app you choose. JPEG/PNG inputs only; normalized PNG strips ancillary metadata, not private information visible in pixels. Cache may disappear." },
            connectivity, new HorizontalStackLayout { Children = { haptics, new Label { Text = "Optional brief haptic after staging", VerticalOptions = LayoutOptions.Center } } },
            text, photo, cancel, clear, preview, status, share, clearExports
        }}};
        SemanticProperties.SetDescription(preview, "Preview of the normalized reference image selected by you");
        RefreshNetwork();
    }
    private async Task PickAsync(bool isPhoto, CancellationToken ct)
    {
        var lease = workspace.Generation; FileResult? selected; picking = true;
        try
        {
            if (isPhoto)
            {
                var results = await MediaPicker.Default.PickPhotosAsync(new MediaPickerOptions
                { SelectionLimit = 1, MaximumWidth = 1600, MaximumHeight = 1600, RotateImage = true, PreserveMetaData = false });
                if (results.Count > 1) throw new InvalidDataException("Select exactly one reference.");
                selected = results.SingleOrDefault();
            }
            else selected = await FilePicker.Default.PickAsync(new PickOptions
            {
                PickerTitle = "Choose a synthetic UTF-8 text reference",
                FileTypes = new FilePickerFileType(new Dictionary<DevicePlatform, IEnumerable<string>>
                { [DevicePlatform.Android] = ["text/plain"], [DevicePlatform.iOS] = ["public.plain-text"] })
            });
        }
        finally { picking = false; }
        ct.ThrowIfCancellationRequested();
        if (selected is null) { status.Text = "Picker canceled; the existing selection is unchanged."; return; }
        var receipt = await workspace.StageAsync(isPhoto ? AttachmentKind.Photo : AttachmentKind.Text, _ => selected.OpenReadAsync(), lease, ct);
        var bytes = await workspace.ReadCurrentAsync(lease, ct); ct.ThrowIfCancellationRequested();
        if (departed || lease != workspace.Generation) throw new OperationCanceledException(ct);
        preview.Source = isPhoto ? ImageSource.FromStream(() => new MemoryStream(bytes, writable: false)) : null;
        status.Text = $"Staged {receipt.Kind}: {receipt.Length} verified bytes. No upload occurred." +
            (workspace.CleanupUnconfirmed ? " Earlier cache cleanup remains unconfirmed." : "");
        if (foreground) _ = CapabilityFallback.RequestOptionalHaptic(haptics.IsToggled, () => HapticFeedback.Default.Perform(HapticFeedbackType.Click));
    }
    private async Task RunAsync(Func<CancellationToken, Task> action)
    {
        if (pending is not null || departed) return;
        using var cancellation = new CancellationTokenSource(); pending = cancellation;
        text.IsEnabled = photo.IsEnabled = share.IsEnabled = false;
        try { await action(cancellation.Token); }
        catch (OperationCanceledException) { if (!departed) status.Text = workspace.Current is null ? "Preparation canceled; no staged attachment." : "Preparation canceled; prior staged attachment remains."; }
        catch (FeatureNotSupportedException) { status.Text = "This device/provider does not support the operation. The local draft can continue without an attachment."; }
        catch (PermissionException) { status.Text = "Access was not granted. Keep the draft without this optional reference."; }
        catch { if (!departed) status.Text = "Reference not confirmed. Check format, dimensions, byte budget, provider access and the lab worksheet. Earlier selection may remain."; }
        finally { pending = null; text.IsEnabled = photo.IsEnabled = share.IsEnabled = !departed; }
    }
    protected override void OnAppearing()
    {
        base.OnAppearing(); if (attached || departed) return; attached = true; foreground = true;
        observedWindow = Window; observedShell = Shell.Current;
        if (observedWindow is not null) { observedWindow.Stopped += Stopped; observedWindow.Resumed += Resumed; observedWindow.Destroying += Destroying; }
        if (observedShell is not null) observedShell.Navigated += Navigated;
        try { Connectivity.Current.ConnectivityChanged += NetworkChanged; networkSubscribed = true; } catch { connectivity.Text = "Connectivity events unavailable; local staging remains available."; }
    }
    private void Stopped(object? sender, EventArgs e) { foreground = false; if (!picking) pending?.Cancel(); }
    private void Resumed(object? sender, EventArgs e) { foreground = true; RefreshNetwork(); }
    private void Destroying(object? sender, EventArgs e) => Leave();
    private void Navigated(object? sender, ShellNavigatedEventArgs e) { if (observedShell?.CurrentPage != this) Leave(); }
    private void NetworkChanged(object? sender, ConnectivityChangedEventArgs e) => Dispatcher.Dispatch(RefreshNetwork);
    private void RefreshNetwork()
    {
        try { connectivity.Text = CapabilityFallback.NetworkHint(Connectivity.Current.NetworkAccess == NetworkAccess.Internet); }
        catch { connectivity.Text = "Connectivity information unavailable. Local staging remains available."; }
    }
    private async void Leave()
    {
        if (departed) return; departed = true; pending?.Cancel(); preview.Source = null;
        if (networkSubscribed) { try { Connectivity.Current.ConnectivityChanged -= NetworkChanged; } catch { } networkSubscribed = false; }
        if (observedShell is not null) observedShell.Navigated -= Navigated;
        if (observedWindow is not null) { observedWindow.Stopped -= Stopped; observedWindow.Resumed -= Resumed; observedWindow.Destroying -= Destroying; }
        try { await workspace.ClearAsync().WaitAsync(TimeSpan.FromSeconds(5)); }
        catch { /* Scope is invalid immediately; delayed/failed physical cleanup is not an erasure claim. */ }
    }
}

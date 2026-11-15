using GnazzosWorld.App.Media;
using GnazzosWorld.Core.Media;

namespace GnazzosWorld.App.Pages;

/// <summary>The mounted lab owns the native host; arbitrary Shell navigation ends its lifetime.</summary>
public sealed class BackgroundAudioLabPage : ContentPage
{
    private readonly BackgroundPlaybackOwner owner;
    private readonly NativeAudioInterruptionMonitor interruptions;
    private readonly IDispatcherTimer timer;
    private readonly Label status = new();
    private readonly Label evidence = new();
    private readonly Picker queue = new() { Title = "Choose a public timing fixture" };
    private readonly Button load = new() { Text = "Load selected (no autoplay)" };
    private readonly Button play = new() { Text = "Play" };
    private readonly Button pause = new() { Text = "Pause and save" };
    private readonly Button back = new() { Text = "Back 5 seconds" };
    private readonly Button forward = new() { Text = "Forward 5 seconds" };
    private Window? observedWindow;
    private Shell? observedShell;
    private bool attached;
    private bool released;
    private string actionError = "";
    public BackgroundAudioLabPage(AudioProgressDatabase progress)
    {
        Title = "Background audio lab";
        var host = new ContentView();
        owner = new(new ToolkitAudioPlayer(host, allowBackground: true), progress, BackgroundDemoAudio.Items);
        interruptions = new(() => MainThread.BeginInvokeOnMainThread(() => { if (!released) Run(owner.Pause); }));
        queue.ItemsSource = BackgroundDemoAudio.Items.ToList(); queue.SelectedIndex = 0;
        var allow = new CheckBox();
        var browse = new VerticalStackLayout { IsVisible = false, Children =
        {
            new Label { Text = "Browse inside this lab", FontSize = 22 },
            new Label { Text = "Minnesota Bob: imagine browsing a public episode description here. The player host stays mounted. This panel is not Shell navigation." }
        }};
        var toggle = new Button { Text = "Show/hide lab browse panel" };
        toggle.Clicked += (_, _) => browse.IsVisible = !browse.IsVisible;
        var stop = new Button { Text = "Stop and release system session" };
        Content = new ScrollView { Content = new VerticalStackLayout { Padding = 24, Spacing = 12, Children =
        {
            new Label { Text = "Background playback and system controls", FontSize = 25 },
            new Label { Text = "Original 60-second timing signal only. Load and Play in the foreground. Opt in below to allow existing playback while this lab is backgrounded. Leaving the lab stops it. Returning never starts audio. Native system controls and interruption behavior require device verification." },
            queue, load, host,
            new HorizontalStackLayout { Children = { allow, new Label { Text = "Allow background continuation", VerticalOptions = LayoutOptions.Center } } },
            status, evidence, play, pause, back, forward, stop, toggle, browse
        }}};
        allow.CheckedChanged += (_, e) => Run(() => owner.SetBackgroundAllowed(e.Value));
        load.Clicked += async (_, _) => await RunAsync(() => owner.LoadAsync(queue.SelectedIndex));
        play.Clicked += (_, _) => Run(owner.Play);
        pause.Clicked += (_, _) => Run(owner.Pause);
        back.Clicked += async (_, _) => await RunAsync(() => owner.SeekAsync(owner.Session.Position - TimeSpan.FromSeconds(5)));
        forward.Clicked += async (_, _) => await RunAsync(() => owner.SeekAsync(owner.Session.Position + TimeSpan.FromSeconds(5)));
        stop.Clicked += (_, _) => Run(owner.Stop);
        owner.Changed += Refresh;
        timer = Dispatcher.CreateTimer(); timer.Interval = TimeSpan.FromSeconds(5);
        timer.Tick += (_, _) => { if (!released && owner.InForeground && !owner.Busy) Run(owner.Checkpoint); };
        SemanticProperties.SetDescription(status, "Native observed playback state and timeline");
        Refresh();
    }
    protected override void OnAppearing()
    {
        base.OnAppearing();
        if (attached || released) return;
        attached = true; observedWindow = Window; observedShell = Shell.Current;
        if (observedWindow is not null)
        {
            observedWindow.Stopped += OnStopped; observedWindow.Resumed += OnResumed;
            observedWindow.Destroying += OnDestroying;
        }
        if (observedShell is not null) observedShell.Navigated += OnNavigated;
        timer.Start();
    }
    private void OnStopped(object? sender, EventArgs e) { timer.Stop(); Run(() => owner.SetForeground(false)); }
    private void OnResumed(object? sender, EventArgs e) { if (!released) { Run(() => owner.SetForeground(true)); timer.Start(); } }
    private void OnDestroying(object? sender, EventArgs e) => Release();
    private void OnNavigated(object? sender, ShellNavigatedEventArgs e)
    {
        // Internal browse is a panel. Every Shell departure deliberately relinquishes ownership.
        if (observedShell?.CurrentPage != this) Release();
    }
    private void Release()
    {
        if (released) return;
        released = true; timer.Stop();
        if (observedWindow is not null)
        {
            observedWindow.Stopped -= OnStopped; observedWindow.Resumed -= OnResumed;
            observedWindow.Destroying -= OnDestroying;
        }
        if (observedShell is not null) observedShell.Navigated -= OnNavigated;
        interruptions.Dispose(); owner.Dispose();
    }
    private void Run(Action action)
    {
        if (released) return;
        try { actionError = ""; action(); }
        catch { actionError = "The native action could not be confirmed. Stop or reload from the foreground."; }
        Refresh();
    }
    private async Task RunAsync(Func<Task> action)
    {
        if (released || owner.Busy) return;
        try { actionError = ""; await action(); }
        catch (OperationCanceledException) { }
        catch { actionError = "The media action failed. Reload deliberately from the foreground."; }
        if (!released) Refresh();
    }
    private void Refresh()
    {
        var s = owner.Session;
        status.Text = $"{s.State}: {s.Current?.Title}\n{s.Position.TotalSeconds:0.0} / {s.Duration.TotalSeconds:0.0} seconds\n{owner.Message}";
        evidence.Text = actionError + (owner.SaveUnconfirmed ? " Progress save unconfirmed; earlier checkpoint may return." : "") +
            (owner.CleanupUnconfirmed ? " Native cleanup unconfirmed; verify on device." : "");
        queue.IsEnabled = load.IsEnabled = !owner.Busy && owner.InForeground;
        play.IsEnabled = !owner.Busy && s.CanPlay;
        pause.IsEnabled = !owner.Busy && s.State is PlaybackState.Playing or PlaybackState.Paused or PlaybackState.Buffering;
        back.IsEnabled = forward.IsEnabled = !owner.Busy && s.CanSeek;
    }
}

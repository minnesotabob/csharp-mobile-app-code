using GnazzosWorld.Core.Orders;
using GnazzosWorld.Core.Presentation;

namespace GnazzosWorld.App.Pages;

// Code-built controls make step transitions explicit in this teaching milestone.
public sealed class SongRequestPage : ContentPage
{
    private readonly SongRequestViewModel vm;
    private readonly SongSubmissionViewModel submission;
    private readonly VerticalStackLayout panel = new() { Padding = 24, Spacing = 12 };
    public SongRequestPage(SongRequestViewModel model, SongSubmissionViewModel submissionModel)
    {
        vm = model; submission = submissionModel; Title = "Custom song";
        Content = new ScrollView { Content = panel };
    }
    protected override void OnAppearing()
    { base.OnAppearing(); vm.PropertyChanged += ReviewChanged; vm.Restore(); Draw(); }
    protected override void OnDisappearing()
    { vm.PropertyChanged -= ReviewChanged; base.OnDisappearing(); }
    private void ReviewChanged(object? sender, System.ComponentModel.PropertyChangedEventArgs e)
    { if (e.PropertyName == nameof(vm.Review) && vm.Review is null) submission.Invalidate(); }
    private void Button(string text, Action action)
    {
        var button = new Microsoft.Maui.Controls.Button { Text = text };
        button.Clicked += (_, _) => action(); panel.Children.Add(button);
    }
    private Microsoft.Maui.Controls.Button AsyncButton(string text, Func<Task> action)
    {
        var button = new Microsoft.Maui.Controls.Button { Text = text };
        button.Clicked += async (_, _) =>
        {
            panel.IsEnabled = false;
            try { await action(); }
            finally { panel.IsEnabled = true; Draw(); }
        };
        panel.Children.Add(button);
        return button;
    }
    private void Field(string label, string value, int limit,
        Action<string> update, bool multiline = false, bool email = false)
    {
        panel.Children.Add(new Label { Text = label });
        InputView input = multiline
            ? new Editor { AutoSize = EditorAutoSizeOption.TextChanges, MinimumHeightRequest = 120 }
            : new Entry { Keyboard = email ? Keyboard.Email : Keyboard.Default };
        input.MaxLength = limit; input.Text = value;
        SemanticProperties.SetHint(input, label);
        input.TextChanged += (_, args) => update(args.NewTextValue ?? "");
        panel.Children.Add(input);
    }
    private void Draw()
    {
        panel.Children.Clear();
        if (vm.Review is null) submission.Invalidate();
        if (submission.RequiresResolution || submission.Receipt is not null)
        {
            panel.Children.Add(new Label { Text = submission.Status });
            if (submission.RequiresResolution)
                AsyncButton("Retry the same demo submission", () => submission.SubmitAsync());
            if (submission.Receipt is DemoReceipt receipt)
                panel.Children.Add(new Label { Text = $"Demo receipt: {receipt.Id}\n" +
                    $"USD {receipt.Total:F2}. No payment. This fixture lasts until app restart." });
            return;
        }
        var heading = new Label { Text = $"Step {vm.Step + 1} of 3", FontSize = 26 };
        SemanticProperties.SetHeadingLevel(heading, SemanticHeadingLevel.Level1);
        panel.Children.Add(heading);
        panel.Children.Add(new Label { Text = "Demo request builder. Review does not send an order." });
        if (vm.Step == 0)
        {
            panel.Children.Add(new Label { Text = "Length in minutes" });
            var minutes = new Picker { Title = "Length", ItemsSource = new[] { 1, 2, 3, 4, 6, 8 } };
            minutes.SelectedItem = vm.Draft.Minutes;
            minutes.SelectedIndexChanged += (_, _) =>
            { if (minutes.SelectedItem is int n) vm.Edit(vm.Draft with { Minutes = n }); };
            panel.Children.Add(minutes);
            panel.Children.Add(new Label { Text = "Number of songs (same specification)" });
            var quantity = new Picker { Title = "Quantity", ItemsSource = new[] { 1, 2, 3 },
                SelectedItem = vm.Draft.Quantity };
            quantity.SelectedIndexChanged += (_, _) =>
            { if (quantity.SelectedItem is int n) vm.Edit(vm.Draft with { Quantity = n }); };
            panel.Children.Add(quantity);
            panel.Children.Add(new Label { Text = "Include vocals" });
            var vocals = new Switch { IsToggled = vm.Draft.WithVocals };
            vocals.Toggled += (_, e) => { vm.Edit(vm.Draft with { WithVocals = e.Value }); Draw(); };
            panel.Children.Add(vocals);
            Field("Genre", vm.Draft.Genre, 80, s => vm.Edit(vm.Draft with { Genre = s }));
            Field("Occasion", vm.Draft.Occasion, 120, s => vm.Edit(vm.Draft with { Occasion = s }));
            panel.Children.Add(new Label { Text = "Allow explicit content (off means clean)" });
            var explicitChoice = new Switch { IsToggled = vm.Draft.AllowExplicit };
            explicitChoice.Toggled += (_, e) => vm.Edit(vm.Draft with { AllowExplicit = e.Value });
            panel.Children.Add(explicitChoice);
            if (vm.Draft.WithVocals)
                Field("Vocal notes (optional)", vm.Draft.VocalNotes, 500,
                    s => vm.Edit(vm.Draft with { VocalNotes = s }));
        }
        else if (vm.Step == 1)
            Field("Your story", vm.Draft.Story, 4000,
                s => vm.Edit(vm.Draft with { Story = s }), multiline: true);
        else
        {
            Field("Contact name", vm.Draft.ContactName, 120,
                s => vm.Edit(vm.Draft with { ContactName = s }));
            Field("Email", vm.Draft.Email, 254,
                s => vm.Edit(vm.Draft with { Email = s }), email: true);
        }
        var errors = new Label { TextColor = Colors.DarkRed };
        errors.SetBinding(Label.TextProperty, new Binding(nameof(vm.Errors), source: vm));
        panel.Children.Add(errors);
        if (vm.Review is SongRequest r)
        {
            var summary = new Label { Text = $"Review: {r.Quantity} song(s), {r.Minutes} minutes each, " +
                $"{(r.WithVocals ? "vocals" : "instrumental")}, {r.Genre}.\n" +
                $"Occasion: {r.Occasion}\nExplicit allowed: {r.AllowExplicit}\n" +
                $"Vocal notes: {r.VocalNotes}\nStory: {r.Story}\n" +
                $"Contact: {r.ContactName}, {r.Email}" };
            // Editing invalidates the visible review immediately.
            summary.SetBinding(IsVisibleProperty, new Binding(nameof(vm.Review),
                converter: new HasReviewConverter(), source: vm));
            panel.Children.Add(summary);
            var quoteButton = AsyncButton("Get demo quote", () => submission.PrepareAsync(r));
            quoteButton.SetBinding(IsVisibleProperty, new Binding(nameof(vm.Review),
                converter: new HasReviewConverter(), source: vm));
            if (submission.Quote is SongQuote quote)
            {
                var quotePanel = new VerticalStackLayout { Spacing = 8 };
                quotePanel.Children.Add(new Label { Text = $"Demo total: USD {quote.Total:F2}\n" +
                    $"Pricing: {quote.PricingVersion}\nExpires: {quote.ExpiresAt:O}\n" +
                    "Example song-service amount only; taxes and fees are not modeled." });
                var submit = new Microsoft.Maui.Controls.Button { Text = "Simulate submission" };
                submit.Clicked += async (_, _) =>
                {
                    panel.IsEnabled = false;
                    try { await submission.SubmitAsync(); }
                    finally { panel.IsEnabled = true; Draw(); }
                };
                submit.SetBinding(IsEnabledProperty, new Binding(nameof(submission.CanSubmit), source: submission));
                quotePanel.Children.Add(submit);
                // Hide a quote as soon as any field invalidates the reviewed snapshot.
                quotePanel.SetBinding(IsVisibleProperty, new Binding(nameof(vm.Review),
                    converter: new HasReviewConverter(), source: vm));
                panel.Children.Add(quotePanel);
            }
            var submissionStatus = new Label();
            submissionStatus.SetBinding(Label.TextProperty, new Binding(nameof(submission.Status), source: submission));
            panel.Children.Add(submissionStatus);
        }
        if (vm.Step > 0) Button("Back", () => { vm.Back(); Draw(); });
        Button(vm.Step == 2 ? "Review request" : "Next", () =>
        {
            if (vm.Next()) Draw();
            else SemanticScreenReader.Default.Announce(vm.Errors);
        });
        Button("Save draft on this device", () => { vm.Save(); });
        Button("Discard draft", async () =>
        {
            if (await DisplayAlertAsync("Discard draft?", "Remove saved and open edits?", "Discard", "Keep"))
            { vm.Discard(); Draw(); }
        });
        var status = new Label();
        status.SetBinding(Label.TextProperty, new Binding(nameof(vm.Status), source: vm));
        panel.Children.Add(status);
    }
    private sealed class HasReviewConverter : IValueConverter
    {
        public object Convert(object? value, Type targetType, object? parameter,
            System.Globalization.CultureInfo culture) => value is SongRequest;
        public object ConvertBack(object? value, Type targetType, object? parameter,
            System.Globalization.CultureInfo culture) => throw new NotSupportedException();
    }
}

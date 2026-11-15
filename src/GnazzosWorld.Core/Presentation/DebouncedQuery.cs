namespace GnazzosWorld.Core.Presentation;

// Call UpdateAsync and Cancel from one serialized context (the page's UI thread).
// The injectable delay makes overlap tests deterministic without real sleeps.
public sealed class DebouncedQuery
{
    private readonly Func<TimeSpan, CancellationToken, Task> delay;
    private readonly TimeSpan interval;
    private CancellationTokenSource? active;
    private long revision;

    public DebouncedQuery(
        Func<TimeSpan, CancellationToken, Task>? delay = null,
        TimeSpan? interval = null)
    {
        this.delay = delay ?? ((time, token) => Task.Delay(time, token));
        this.interval = interval ?? TimeSpan.FromMilliseconds(250);
        if (this.interval < TimeSpan.Zero)
            throw new ArgumentOutOfRangeException(nameof(interval));
    }

    public async Task UpdateAsync(string? text, Action<string> apply, Action reportError)
    {
        ArgumentNullException.ThrowIfNull(apply);
        ArgumentNullException.ThrowIfNull(reportError);
        var current = ++revision;
        active?.Cancel();
        using var own = new CancellationTokenSource();
        active = own;
        try
        {
            // Clearing is immediate and still invalidates older pending input.
            if (!string.IsNullOrEmpty(text))
                await delay(interval, own.Token);
            if (!own.IsCancellationRequested && current == revision)
                apply(text ?? "");
        }
        catch (OperationCanceledException) when (own.IsCancellationRequested) { }
        catch (Exception)
        {
            if (!own.IsCancellationRequested && current == revision) reportError();
        }
        finally
        {
            if (ReferenceEquals(active, own)) active = null;
        }
    }

    public void Cancel()
    {
        ++revision;
        active?.Cancel();
    }
}

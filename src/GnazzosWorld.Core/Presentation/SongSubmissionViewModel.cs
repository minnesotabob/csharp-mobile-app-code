using GnazzosWorld.Core.Orders;

namespace GnazzosWorld.Core.Presentation;

// Single active UI flow. Durable pending operations and account scoping arrive later.
public sealed class SongSubmissionViewModel(ISongSubmissionService service) : ObservableObject
{
    private int generation;
    private SubmissionAttempt? attempt;
    public SongQuote? Quote { get; private set; }
    public DemoReceipt? Receipt { get; private set; }
    public bool IsBusy { get; private set; }
    public bool RequiresResolution { get; private set; }
    public bool CanSubmit => !IsBusy && attempt is not null && Receipt is null;
    public string Status { get; private set; } = "Get a demo quote after reviewing your request.";

    public void Invalidate()
    {
        if (RequiresResolution || Receipt is not null) return;
        generation++; attempt = null; Quote = null; Receipt = null;
        Status = "Request changed. Review it and get a new demo quote."; Changed();
    }
    public async Task PrepareAsync(SongRequest review, CancellationToken cancellationToken = default)
    {
        if (IsBusy || RequiresResolution || Receipt is not null) return;
        Invalidate(); var owner = generation;
        IsBusy = true; Status = "Calculating demo quote..."; Changed();
        try
        {
            var quote = await service.QuoteAsync(review, cancellationToken);
            if (owner != generation) return;
            Quote = quote; attempt = new(Guid.NewGuid(), quote.Id, quote.Request);
            Status = "Demo quote ready. No payment will be taken.";
        }
        catch (SubmissionRejectedException) when (owner == generation)
        { Status = "Request could not be quoted. Check the form and review again."; }
        catch (Exception ex) when (ex is IOException or TimeoutException or OperationCanceledException)
        { if (owner == generation) Status = "Could not get a quote. Your draft is unchanged; try again."; }
        finally { IsBusy = false; Changed(); }
    }
    public async Task SubmitAsync(CancellationToken cancellationToken = default)
    {
        if (!CanSubmit) return;
        var pending = attempt!; var owner = generation;
        IsBusy = true; Status = "Submitting to the demo service..."; Changed();
        try
        {
            var receipt = await service.SubmitAsync(pending, cancellationToken);
            if (owner != generation) return;
            Receipt = receipt; RequiresResolution = false;
            Status = "Demo request accepted. No order was sent and no payment was taken.";
        }
        catch (SubmissionRejectedException ex) when (owner == generation)
        {
            attempt = null; RequiresResolution = false;
            Status = ex.Problem == SubmissionProblem.ExpiredQuote
                ? "Quote expired. Get a new demo quote and review the total."
                : "Demo submission was rejected. Review the request and get a new quote.";
        }
        catch (Exception ex) when (ex is IOException or TimeoutException or OperationCanceledException)
        {
            if (owner == generation)
            {
                RequiresResolution = true;
                Status = "Outcome unknown. Retry this same demo submission to check its result.";
            }
            // Preserve the exact key/quote/request even when cancellation follows acceptance.
        }
        finally { IsBusy = false; Changed(); }
    }
    private void Changed()
    {
        foreach (var name in new[] { nameof(Quote), nameof(Receipt), nameof(IsBusy), nameof(RequiresResolution), nameof(CanSubmit), nameof(Status) })
            Notify(name);
    }
}

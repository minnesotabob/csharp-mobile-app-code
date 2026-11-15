namespace GnazzosWorld.Core.Orders;

public sealed record SongQuote(
    Guid Id, SongRequest Request, string PricingVersion,
    string Currency, decimal Total, DateTimeOffset ExpiresAt);

// The caller supplies no trusted price, status, owner, or payment fields.
public sealed record SubmissionAttempt(Guid Key, Guid QuoteId, SongRequest Request);
public sealed record DemoReceipt(Guid Id, Guid QuoteId, string Currency, decimal Total);

public enum SubmissionProblem { InvalidRequest, UnknownQuote, ExpiredQuote, ChangedRequest, KeyConflict, QuoteUsed }
public sealed class SubmissionRejectedException(SubmissionProblem problem) : Exception(problem.ToString())
{
    public SubmissionProblem Problem { get; } = problem;
}

public interface ISongSubmissionService
{
    Task<SongQuote> QuoteAsync(SongRequest request, CancellationToken cancellationToken = default);
    Task<DemoReceipt> SubmitAsync(SubmissionAttempt attempt, CancellationToken cancellationToken = default);
}

// Process-local teaching fixture. No HTTP, identity, database, checkout, or live order.
// The lock protects one instance only; production acceptance needs a database transaction.
public sealed class DemoSongSubmissionService(TimeProvider clock) : ISongSubmissionService
{
    private readonly object gate = new();
    private readonly Dictionary<Guid, SongQuote> quotes = new();
    private readonly Dictionary<Guid, (SubmissionAttempt Attempt, DemoReceipt Receipt)> accepted = new();
    private readonly HashSet<Guid> usedQuotes = new();

    public Task<SongQuote> QuoteAsync(SongRequest request, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        if (SongRequestRules.Validate(request, 2).Count != 0)
            throw new SubmissionRejectedException(SubmissionProblem.InvalidRequest);
        var normalized = request.ForReview();
        var quote = new SongQuote(Guid.NewGuid(), normalized, SongPricing.Version,
            "USD", SongPricing.Calculate(normalized.Minutes, normalized.WithVocals, normalized.Quantity),
            clock.GetUtcNow().AddMinutes(15));
        lock (gate) quotes.Add(quote.Id, quote);
        return Task.FromResult(quote);
    }

    public Task<DemoReceipt> SubmitAsync(SubmissionAttempt attempt, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        lock (gate)
        {
            // Replay BEFORE expiry: an accepted operation remains accepted after its quote expires.
            if (accepted.TryGetValue(attempt.Key, out var previous))
            {
                if (previous.Attempt != attempt)
                    throw new SubmissionRejectedException(SubmissionProblem.KeyConflict);
                return Task.FromResult(previous.Receipt);
            }
            if (attempt.Key == Guid.Empty || SongRequestRules.Validate(attempt.Request, 2).Count != 0)
                throw new SubmissionRejectedException(SubmissionProblem.InvalidRequest);
            if (!quotes.TryGetValue(attempt.QuoteId, out var quote))
                throw new SubmissionRejectedException(SubmissionProblem.UnknownQuote);
            if (attempt.Request != quote.Request)
                throw new SubmissionRejectedException(SubmissionProblem.ChangedRequest);
            if (usedQuotes.Contains(quote.Id))
                throw new SubmissionRejectedException(SubmissionProblem.QuoteUsed);
            if (clock.GetUtcNow() >= quote.ExpiresAt)
                throw new SubmissionRejectedException(SubmissionProblem.ExpiredQuote);
            var receipt = new DemoReceipt(Guid.NewGuid(), quote.Id, quote.Currency, quote.Total);
            accepted.Add(attempt.Key, (attempt, receipt));
            usedQuotes.Add(quote.Id);
            return Task.FromResult(receipt);
        }
    }
}

using GnazzosWorld.Core.Orders;
using GnazzosWorld.Core.Presentation;
using Xunit;

namespace GnazzosWorld.Core.Tests;

public sealed class SubmissionTests
{
    private static SongRequest Valid() => new()
    {
        Genre = "Folk", Occasion = "Anniversary", Story = "A fictional lakeside celebration",
        ContactName = "Demo Customer", Email = "demo@example.com"
    };
    private sealed class Clock : TimeProvider
    {
        public DateTimeOffset Now = new(2026, 11, 15, 12, 0, 0, TimeSpan.Zero);
        public override DateTimeOffset GetUtcNow() => Now;
    }
    private static SubmissionAttempt Attempt(SongQuote q) => new(Guid.NewGuid(), q.Id, q.Request);
    private static async Task Rejected(SubmissionProblem problem, Func<Task> action)
        => Assert.Equal(problem, (await Assert.ThrowsAsync<SubmissionRejectedException>(action)).Problem);

    [Theory]
    [InlineData(1, false, 2, 9310)] [InlineData(1, true, 3, 21330)]
    [InlineData(2, false, 3, 21330)] [InlineData(2, true, 2, 24510)]
    [InlineData(3, false, 3, 29430)] [InlineData(3, true, 2, 34010)]
    [InlineData(4, false, 2, 26410)] [InlineData(4, true, 3, 61830)]
    [InlineData(6, false, 3, 53730)] [InlineData(6, true, 2, 60610)]
    [InlineData(8, false, 2, 49210)] [InlineData(8, true, 3, 107730)]
    public async Task Quote_has_version_currency_and_snapshot_amount(int minutes, bool voice, int quantity, int cents)
    {
        var clock = new Clock(); var service = new DemoSongSubmissionService(clock);
        var quote = await service.QuoteAsync(Valid() with { Minutes = minutes, WithVocals = voice, Quantity = quantity }, TestContext.Current.CancellationToken);
        Assert.Equal(cents / 100m, quote.Total); Assert.Equal("USD", quote.Currency);
        Assert.Equal(SongPricing.Version, quote.PricingVersion);
        Assert.Equal(clock.Now.AddMinutes(15), quote.ExpiresAt);
    }
    [Theory]
    [InlineData(0)] [InlineData(4)]
    public async Task Invalid_quantity_is_rejected_before_quote(int quantity)
        => await Rejected(SubmissionProblem.InvalidRequest, () =>
            new DemoSongSubmissionService(new Clock()).QuoteAsync(Valid() with { Quantity = quantity }, TestContext.Current.CancellationToken));
    [Fact]
    public async Task Invalid_contact_is_revalidated_at_service_boundary()
        => await Rejected(SubmissionProblem.InvalidRequest, () =>
            new DemoSongSubmissionService(new Clock()).QuoteAsync(Valid() with { Email = "bad" }, TestContext.Current.CancellationToken));
    [Fact]
    public async Task Quote_normalizes_without_mutating_draft()
    {
        var request = Valid() with { Genre = " Folk ", WithVocals = false, VocalNotes = "Hidden" };
        var quote = await new DemoSongSubmissionService(new Clock()).QuoteAsync(request, TestContext.Current.CancellationToken);
        Assert.Equal("Folk", quote.Request.Genre); Assert.Equal("", quote.Request.VocalNotes);
        Assert.Equal("Hidden", request.VocalNotes);
    }
    [Fact]
    public async Task Expiration_includes_exact_boundary()
    {
        var clock = new Clock(); var service = new DemoSongSubmissionService(clock);
        var quote = await service.QuoteAsync(Valid(), TestContext.Current.CancellationToken); clock.Now = quote.ExpiresAt;
        await Rejected(SubmissionProblem.ExpiredQuote, () => service.SubmitAsync(Attempt(quote), TestContext.Current.CancellationToken));
    }
    [Fact]
    public async Task Quote_can_be_accepted_just_before_expiration()
    {
        var clock = new Clock(); var service = new DemoSongSubmissionService(clock);
        var quote = await service.QuoteAsync(Valid(), TestContext.Current.CancellationToken); clock.Now = quote.ExpiresAt.AddTicks(-1);
        Assert.Equal(quote.Id, (await service.SubmitAsync(Attempt(quote), TestContext.Current.CancellationToken)).QuoteId);
    }
    [Fact]
    public async Task Same_key_and_payload_replay_same_receipt_even_after_expiry()
    {
        var clock = new Clock(); var service = new DemoSongSubmissionService(clock);
        var quote = await service.QuoteAsync(Valid(), TestContext.Current.CancellationToken); var attempt = Attempt(quote);
        var receipt = await service.SubmitAsync(attempt, TestContext.Current.CancellationToken); clock.Now = quote.ExpiresAt.AddDays(1);
        Assert.Equal(receipt, await service.SubmitAsync(attempt, TestContext.Current.CancellationToken));
    }
    [Fact]
    public async Task Reusing_key_with_changed_payload_is_conflict()
    {
        var service = new DemoSongSubmissionService(new Clock()); var quote = await service.QuoteAsync(Valid(), TestContext.Current.CancellationToken);
        var attempt = Attempt(quote); await service.SubmitAsync(attempt, TestContext.Current.CancellationToken);
        await Rejected(SubmissionProblem.KeyConflict, () => service.SubmitAsync(attempt with
            { Request = attempt.Request with { Story = "Changed" } }, TestContext.Current.CancellationToken));
    }
    [Fact]
    public async Task Changed_request_requires_new_quote()
    {
        var service = new DemoSongSubmissionService(new Clock()); var quote = await service.QuoteAsync(Valid(), TestContext.Current.CancellationToken);
        await Rejected(SubmissionProblem.ChangedRequest, () => service.SubmitAsync(Attempt(quote) with
            { Request = quote.Request with { Quantity = 2 } }, TestContext.Current.CancellationToken));
    }
    [Fact]
    public async Task Unknown_quote_is_rejected()
        => await Rejected(SubmissionProblem.UnknownQuote, () => new DemoSongSubmissionService(new Clock())
            .SubmitAsync(new(Guid.NewGuid(), Guid.NewGuid(), Valid()), TestContext.Current.CancellationToken));
    [Fact]
    public async Task Empty_key_is_rejected()
    {
        var service = new DemoSongSubmissionService(new Clock()); var q = await service.QuoteAsync(Valid(), TestContext.Current.CancellationToken);
        await Rejected(SubmissionProblem.InvalidRequest, () => service.SubmitAsync(Attempt(q) with { Key = Guid.Empty }, TestContext.Current.CancellationToken));
    }
    [Fact]
    public async Task Another_key_cannot_accept_the_same_quote_twice()
    {
        var service = new DemoSongSubmissionService(new Clock()); var q = await service.QuoteAsync(Valid(), TestContext.Current.CancellationToken);
        await service.SubmitAsync(Attempt(q), TestContext.Current.CancellationToken);
        await Rejected(SubmissionProblem.QuoteUsed, () => service.SubmitAsync(Attempt(q), TestContext.Current.CancellationToken));
    }
    [Fact]
    public async Task Concurrent_duplicate_deliveries_share_one_receipt()
    {
        var service = new DemoSongSubmissionService(new Clock()); var q = await service.QuoteAsync(Valid(), TestContext.Current.CancellationToken);
        var attempt = Attempt(q);
        var receipts = await Task.WhenAll(Enumerable.Range(0, 16).Select(_ => Task.Run(() => service.SubmitAsync(attempt, TestContext.Current.CancellationToken))));
        Assert.Single(receipts.Select(r => r.Id).Distinct());
    }
    private sealed class LostResponse(ISongSubmissionService inner) : ISongSubmissionService
    {
        public readonly List<SubmissionAttempt> Calls = new();
        public Task<SongQuote> QuoteAsync(SongRequest r, CancellationToken ct = default) => inner.QuoteAsync(r, ct);
        public async Task<DemoReceipt> SubmitAsync(SubmissionAttempt a, CancellationToken ct = default)
        {
            Calls.Add(a); var receipt = await inner.SubmitAsync(a, ct);
            if (Calls.Count == 1) throw new IOException("Simulated lost response");
            return receipt;
        }
    }
    [Fact]
    public async Task Lost_response_preserves_exact_operation_and_blocks_requote()
    {
        var adapter = new LostResponse(new DemoSongSubmissionService(new Clock()));
        var vm = new SongSubmissionViewModel(adapter); await vm.PrepareAsync(Valid(), TestContext.Current.CancellationToken);
        await vm.SubmitAsync(TestContext.Current.CancellationToken); Assert.True(vm.RequiresResolution); Assert.Null(vm.Receipt);
        var quote = vm.Quote; vm.Invalidate(); await vm.PrepareAsync(Valid() with { Quantity = 2 }, TestContext.Current.CancellationToken);
        Assert.Equal(quote, vm.Quote);
        await vm.SubmitAsync(TestContext.Current.CancellationToken); Assert.NotNull(vm.Receipt); Assert.False(vm.RequiresResolution);
        Assert.Equal(adapter.Calls[0], adapter.Calls[1]); Assert.False(vm.CanSubmit);
        await vm.SubmitAsync(TestContext.Current.CancellationToken); Assert.Equal(2, adapter.Calls.Count);
    }
    [Fact]
    public async Task Draft_edit_invalidates_unsent_quote()
    {
        var vm = new SongSubmissionViewModel(new DemoSongSubmissionService(new Clock()));
        await vm.PrepareAsync(Valid(), TestContext.Current.CancellationToken); vm.Invalidate(); Assert.Null(vm.Quote); Assert.False(vm.CanSubmit);
    }
    [Fact]
    public async Task Expiry_rejection_requires_explicit_requote()
    {
        var clock = new Clock(); var vm = new SongSubmissionViewModel(new DemoSongSubmissionService(clock));
        await vm.PrepareAsync(Valid(), TestContext.Current.CancellationToken); var old = vm.Quote!; clock.Now = old.ExpiresAt;
        await vm.SubmitAsync(TestContext.Current.CancellationToken); Assert.False(vm.CanSubmit); Assert.False(vm.RequiresResolution);
        await vm.PrepareAsync(Valid(), TestContext.Current.CancellationToken); Assert.NotEqual(old.Id, vm.Quote!.Id); Assert.True(vm.CanSubmit);
    }
    private sealed class Delayed(ISongSubmissionService inner) : ISongSubmissionService
    {
        public int Calls;
        public TaskCompletionSource<bool> Release = new(TaskCreationOptions.RunContinuationsAsynchronously);
        public Task<SongQuote> QuoteAsync(SongRequest r, CancellationToken ct = default) => inner.QuoteAsync(r, ct);
        public async Task<DemoReceipt> SubmitAsync(SubmissionAttempt a, CancellationToken ct = default)
        { Calls++; await Release.Task; return await inner.SubmitAsync(a, ct); }
    }
    [Fact]
    public async Task Overlapping_taps_only_call_service_once()
    {
        var adapter = new Delayed(new DemoSongSubmissionService(new Clock()));
        var vm = new SongSubmissionViewModel(adapter); await vm.PrepareAsync(Valid(), TestContext.Current.CancellationToken);
        var first = vm.SubmitAsync(TestContext.Current.CancellationToken); await vm.SubmitAsync(TestContext.Current.CancellationToken); Assert.Equal(1, adapter.Calls);
        adapter.Release.SetResult(true); await first; Assert.NotNull(vm.Receipt); Assert.False(vm.IsBusy);
    }
    [Fact]
    public async Task Cancellation_before_acceptance_has_no_receipt_and_can_retry()
    {
        var vm = new SongSubmissionViewModel(new DemoSongSubmissionService(new Clock()));
        await vm.PrepareAsync(Valid(), TestContext.Current.CancellationToken); using var cts = new CancellationTokenSource(); cts.Cancel();
        await vm.SubmitAsync(cts.Token); Assert.Null(vm.Receipt); Assert.True(vm.CanSubmit);
        await vm.SubmitAsync(TestContext.Current.CancellationToken); Assert.NotNull(vm.Receipt);
    }
}

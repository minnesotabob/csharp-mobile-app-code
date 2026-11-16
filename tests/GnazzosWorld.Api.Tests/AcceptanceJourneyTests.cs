using System.Net;
using System.Net.Http.Json;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using GnazzosWorld.Core.Commerce;
using GnazzosWorld.Core.Fulfillment;
using GnazzosWorld.Core.Identity;
using GnazzosWorld.Core.Orders;
using GnazzosWorld.Core.Payments;
using Xunit;

namespace GnazzosWorld.Api.Tests;

// Composes actual clients, journals, bearer middleware and SQLite through TestServer.
// The injected transport fault happens AFTER the actual endpoint has committed.
public sealed class AcceptanceJourneyTests : IDisposable
{
    private static CancellationToken Ct => TestContext.Current.CancellationToken;
    private const string Secret = "whsec_synthetic_fixture_secret_0123456789";
    private static readonly OrderOwner Owner = new("https://fixture.auth0.com/", "reader-a");
    private readonly string root = Path.Combine(Path.GetTempPath(), "gnazzo-acceptance-" + Guid.NewGuid().ToString("N"));

    [Theory]
    [InlineData("acceptance")]
    [InlineData("revision")]
    public async Task Anniversary_journey_recovers_committed_reply_loss(string boundary)
    {
        var serverPath = Path.Combine(root, "server.db");
        var orderPath = Path.Combine(root, "orders.db");
        var revisionPath = Path.Combine(root, "revisions.db");
        string orderId, operationKey;
        var provider = new CheckoutProvider();
        using (var store = new SqliteOrderStore(serverPath, TimeProvider.System))
        using (var host = new RequestAuthorizationTests.Factory(commerceStore: store, paymentProvider: provider, fulfillment: true))
        using (var boot = host.Client())
        {
            var session = Session(host);
            var lease = await session.AcquireOwnedAsync(Ct);
            var drop = new DropCommittedReply(host.Server.CreateHandler(), "/api/v1/commerce/orders", boundary == "acceptance");
            using (var api = new OrderApiClient(session, drop))
            using (var journal = new PendingOrderDatabase(orderPath))
            {
                var coordinator = new OrderAttemptCoordinator(session, api, journal);
                var quote = await api.QuoteAsync(new SongRequest { Genre = "Folk", Occasion = "Synthetic anniversary", Story = "Invented acceptance story", ContactName = "Sample Reader", Email = "reader@example.test" }, lease, Ct);
                if (boundary == "acceptance")
                    await Assert.ThrowsAsync<HttpRequestException>(() => coordinator.AcceptAsync(quote, lease, Ct));
                else
                    await coordinator.AcceptAsync(quote, lease, Ct);
                var pending = journal.Read(coordinator.Account(lease))!;
                operationKey = pending.OperationKey;
                Assert.Equal(boundary == "acceptance", pending.Confirmed is null);
                Assert.Equal(boundary == "acceptance", drop.Dropped);
                orderId = Assert.Single(store.ListOrders(Owner).Items).Id;
                Assert.Single(store.HistoryForLab(orderId));
            }
            // Recreate the real client and reopen its on-disk journal, not a copied fake receipt.
            using (var api = new OrderApiClient(session, host.Server.CreateHandler()))
            using (var journal = new PendingOrderDatabase(orderPath))
            {
                var coordinator = new OrderAttemptCoordinator(session, api, journal);
                var recovered = await coordinator.ResumeAsync(lease, Ct);
                Assert.Equal(orderId, recovered.Id);
                Assert.Equal(operationKey, recovered.OperationKey);
                Assert.Equal(recovered, journal.Read(coordinator.Account(lease))!.Confirmed);
                Assert.Single(store.ListOrders(Owner).Items);
                Assert.Single(store.HistoryForLab(orderId));
            }
            using var client = host.Client(host.Token(scope: "orders:read orders:write"));
            using var checkout = await client.PostAsync($"/api/v1/commerce/orders/{orderId}/test-checkout", null, Ct);
            Assert.Equal(HttpStatusCode.OK, checkout.StatusCode);
            var payment = (await checkout.Content.ReadFromJsonAsync<TestPaymentReceipt>(OrderContract.Json, Ct))!;
            var order = store.Get(Owner, orderId)!;
            var bytes = JsonSerializer.SerializeToUtf8Bytes(new { id = "evt_acceptance_" + payment.AttemptId, @object = "event", livemode = false, type = "checkout.session.completed", data = new { @object = new { id = payment.SessionId, @object = "checkout.session", livemode = false, mode = "payment", client_reference_id = payment.AttemptId, metadata = new { order_id = orderId }, currency = "usd", amount_total = order.TotalMinor, payment_status = "paid", payment_intent = "pi_acceptance_fixture" } } });
            using var webhook = host.Client();
            await Deliver(webhook, bytes);
            await Deliver(webhook, bytes);
            Assert.Equal(1, provider.Calls);
            Assert.Equal(TestPaymentState.Paid, store.GetTestPayment(Owner, orderId)!.State);
            store.StartProductionForPaymentLab(orderId, 1);
            store.PublishReviewForLab(orderId, 2);

            var revisionRoute = $"/api/v1/commerce/orders/{orderId}/revisions";
            var revisionDrop = new DropCommittedReply(host.Server.CreateHandler(), revisionRoute, boundary == "revision");
            using (var api = new TrackingApiClient(session, revisionDrop))
            using (var journal = new PendingRevisionDatabase(revisionPath))
            {
                var coordinator = new RevisionCoordinator(api, journal);
                if (boundary == "revision")
                    await Assert.ThrowsAsync<HttpRequestException>(() => coordinator.SubmitAsync(orderId, 3, "Invented revision", lease, Ct));
                else
                    await coordinator.SubmitAsync(orderId, 3, "Invented revision", lease, Ct);
                Assert.Equal(boundary == "revision", revisionDrop.Dropped);
                Assert.Equal(boundary == "revision", journal.Read(api.Account(lease), orderId)!.Confirmed is null);
            }
            // Review advances BEFORE replay; replay must precede the stale expected-version check.
            store.PublishReviewForLab(orderId, 4);
            using (var api = new TrackingApiClient(session, host.Server.CreateHandler()))
            using (var journal = new PendingRevisionDatabase(revisionPath))
            {
                var receipt = await new RevisionCoordinator(api, journal).ResumeAsync(orderId, lease, Ct);
                Assert.Equal(4, receipt.AppliedVersion);
                Assert.Equal(receipt, journal.Read(api.Account(lease), orderId)!.Confirmed);
                Assert.Single((await api.TrackAsync(orderId, lease, Ct)).Revisions);
            }
            store.PublishDeliveryForLab(orderId, 5);
        }
        // Reopen server storage and construct a new host/session. No in-memory order fixture survives.
        using var reopened = new SqliteOrderStore(serverPath, TimeProvider.System);
        using var secondHost = new RequestAuthorizationTests.Factory(commerceStore: reopened, fulfillment: true);
        using var secondBoot = secondHost.Client();
        var secondSession = Session(secondHost);
        var secondLease = await secondSession.AcquireOwnedAsync(Ct);
        using var tracking = new TrackingApiClient(secondSession, secondHost.Server.CreateHandler());
        var snapshot = await tracking.TrackAsync(orderId, secondLease, Ct);
        Assert.Equal(6, snapshot.Order.Version);
        Assert.Equal(6, snapshot.History.Count);
        Assert.Equal(OrderState.Delivered, snapshot.Order.State);
        Assert.Equal(operationKey, snapshot.Order.OperationKey);
        Assert.Single(snapshot.Revisions);
        using var verified = await tracking.FetchAsync(orderId, Assert.Single(snapshot.Deliveries), 6, secondLease, Ct);
        Assert.Equal(PrivateDeliveryFixture.Length, verified.Bytes.Length);
        Assert.Equal(PrivateDeliveryFixture.Sha256, Convert.ToHexString(SHA256.HashData(verified.Bytes.Span)).ToLowerInvariant());
        using var foreign = secondHost.Client(secondHost.Token(subject: "reader-b", scope: "orders:read orders:write"));
        using var denied = await foreign.GetAsync($"/api/v1/commerce/orders/{orderId}/tracking", Ct);
        Assert.Equal(HttpStatusCode.NotFound, denied.StatusCode);
        using var journalCheck = new PendingOrderDatabase(orderPath);
        var a = tracking.Account(secondLease);
        Assert.Null(journalCheck.Read(a with { Owner = Owner with { Subject = "reader-b" } }));
        Assert.Equal(orderId, journalCheck.Read(a)!.Confirmed!.Id);
    }

    private static ApiSession Session(RequestAuthorizationTests.Factory host)
    {
        var settings = new ApiSessionSettings(new("fixture.auth0.com", "public-client-id"), "https://gnazzosworld.example/api", new("https://localhost"));
        return new(settings, new Vault(new(settings.Identity.Issuer, "reader-a", settings.Audience, host.Token(scope: "orders:read orders:write"), null, DateTimeOffset.UtcNow.AddMinutes(4))), new UnusedProvider(), TimeProvider.System);
    }
    private static async Task Deliver(HttpClient client, byte[] bytes)
    {
        var t = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
        using var request = new HttpRequestMessage(HttpMethod.Post, "/api/v1/commerce/test-payments/webhook") { Content = new ByteArrayContent(bytes) };
        request.Content.Headers.ContentType = new("application/json");
        request.Headers.Add("Stripe-Signature", $"t={t},v1=" + Convert.ToHexString(HMACSHA256.HashData(Encoding.UTF8.GetBytes(Secret), Encoding.ASCII.GetBytes(t + ".").Concat(bytes).ToArray())));
        using var response = await client.SendAsync(request, Ct);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }
    private sealed class DropCommittedReply(HttpMessageHandler inner, string path, bool enabled) : DelegatingHandler(inner)
    {
        public bool Dropped { get; private set; }
        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken ct)
        {
            var response = await base.SendAsync(request, ct);
            if (enabled && !Dropped && request.Method == HttpMethod.Post && request.RequestUri!.AbsolutePath == path && response.IsSuccessStatusCode)
            { Dropped = true; response.Dispose(); throw new HttpRequestException("Synthetic lost committed reply."); }
            return response;
        }
    }
    private sealed class CheckoutProvider : ITestCheckoutProvider
    {
        public int Calls; public string Scope => "stripe-test:acct_fixture"; public string ReturnOrigin => "https://localhost/";
        public Task<HostedTestSession> CreateAsync(CheckoutReservation r, CancellationToken ct)
        { Calls++; return Task.FromResult(new HostedTestSession("cs_test_" + r.AttemptId, "https://checkout.stripe.com/c/pay/" + r.AttemptId)); }
    }
    private sealed class Vault(SessionGrant initial) : ISessionVault
    { private SessionGrant? value = initial; public Task<SessionGrant?> ReadAsync(CancellationToken ct) => Task.FromResult(value); public Task WriteAsync(SessionGrant? next, CancellationToken ct) { value = next; return Task.CompletedTask; } }
    private sealed class UnusedProvider : ISessionProvider
    { public Task<SessionGrant> SignInAsync(CancellationToken ct) => throw new InvalidOperationException(); public Task<SessionGrant> RefreshAsync(SessionGrant previous, CancellationToken ct) => throw new InvalidOperationException(); }
    public void Dispose() { if (Directory.Exists(root)) Directory.Delete(root, true); }
}

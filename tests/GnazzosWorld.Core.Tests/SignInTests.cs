using GnazzosWorld.Core.Identity;
using Xunit;

namespace GnazzosWorld.Core.Tests;

public sealed class SignInTests
{
    private static CancellationToken Ct => TestContext.Current.CancellationToken;
    private static readonly NativeIdentitySettings Settings = new("demo-reader.us.auth0.com", "public-client-id-example");
    private static SignedInReader Reader(string subject = "auth0|fixture") => new(Settings.Issuer, subject, "Fixture reader");
    private sealed class Provider(Func<CancellationToken, Task<SignInReply>> login) : IInteractiveSignIn
    {
        public Uri? ExpectedIssuer => Settings.Issuer;
        public Task<SignInReply> LoginAsync(CancellationToken ct) => login(ct);
    }
    [Theory]
    [InlineData("https://demo.auth0.com")]
    [InlineData("demo.auth0.com.evil.example")]
    [InlineData("demo.auth0.com/path")]
    [InlineData("demo.auth0.com:443")]
    public void Tenant_config_rejects_a_url_or_unexpected_host(string domain)
        => Assert.Throws<ArgumentException>(() => new NativeIdentitySettings(domain, "public-client-id"));

    [Theory]
    [InlineData("http://demo-reader.us.auth0.com/authorize", NativeIdentitySettings.Callback)]
    [InlineData("https://other.auth0.com/authorize", NativeIdentitySettings.Callback)]
    [InlineData("https://demo-reader.us.auth0.com/authorize", "other://callback/")]
    [InlineData("https://demo-reader.us.auth0.com/token", NativeIdentitySettings.Callback)]
    [InlineData("https://demo-reader.us.auth0.com/authorize", "com.johngnazzo.gnazzosworld://callback")]
    public void Browser_boundary_rejects_changed_destinations(string start, string end)
        => Assert.Throws<InvalidDataException>(() => Settings.ValidateBrowserRequest(start, end));

    [Fact]
    public void Browser_boundary_accepts_sdk_authorize_request_with_query()
        => Settings.ValidateBrowserRequest("https://demo-reader.us.auth0.com/authorize?state=fixture&code_challenge=fixture", NativeIdentitySettings.Callback);

    [Fact]
    public async Task Successful_provider_result_updates_only_process_identity()
    {
        var model = new SignInSession(new Provider(_ => Task.FromResult(new SignInReply(SignInOutcome.Succeeded, Reader()))));
        await model.SignInAsync(Ct);
        Assert.Equal("Fixture reader", model.DisplayName); Assert.False(model.CanStart); Assert.False(model.IsBusy);
        model.ForgetLocalIdentity(); Assert.Null(model.Reader); Assert.True(model.CanStart);
        Assert.Contains("browser session", model.StatusMessage);
    }
    [Theory]
    [InlineData(SignInOutcome.Canceled)]
    [InlineData(SignInOutcome.Rejected)]
    [InlineData(SignInOutcome.Unconfigured)]
    public async Task Incomplete_sign_in_keeps_anonymous_browsing_and_retry(SignInOutcome outcome)
    {
        var model = new SignInSession(new Provider(_ => Task.FromResult(new SignInReply(outcome))));
        await model.SignInAsync(Ct); Assert.Null(model.Reader); Assert.True(model.CanStart); Assert.False(model.IsBusy);
    }
    [Fact]
    public async Task Raw_provider_exception_is_not_presented_as_diagnostic_text()
    {
        var model = new SignInSession(new Provider(_ => throw new InvalidOperationException("token=private-fixture-value")));
        await model.SignInAsync(Ct); Assert.DoesNotContain("private-fixture-value", model.StatusMessage); Assert.Null(model.Reader);
    }
    [Fact]
    public async Task Second_tap_does_not_open_another_browser()
    {
        var entered = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var finish = new TaskCompletionSource<SignInReply>(TaskCreationOptions.RunContinuationsAsynchronously); var calls = 0;
        var model = new SignInSession(new Provider(_ => { Interlocked.Increment(ref calls); entered.SetResult(); return finish.Task; }));
        var first = model.SignInAsync(Ct); await entered.Task.WaitAsync(Ct);
        await model.SignInAsync(Ct); Assert.Equal(1, calls); Assert.True(model.IsBusy);
        finish.SetResult(new(SignInOutcome.Canceled)); await first; Assert.False(model.IsBusy);
    }
    [Fact]
    public async Task Forgetting_local_identity_fences_a_late_success_even_if_provider_ignores_cancel()
    {
        var finish = new TaskCompletionSource<SignInReply>(TaskCreationOptions.RunContinuationsAsynchronously);
        var model = new SignInSession(new Provider(_ => finish.Task));
        var attempt = model.SignInAsync(Ct); model.ForgetLocalIdentity();
        finish.SetResult(new(SignInOutcome.Succeeded, Reader())); await attempt;
        Assert.Null(model.Reader); Assert.True(model.CanStart); Assert.Contains("Local identity cleared", model.StatusMessage);
    }
    [Fact]
    public async Task Cancellation_before_late_success_does_not_publish_identity()
    {
        var finish = new TaskCompletionSource<SignInReply>(TaskCreationOptions.RunContinuationsAsynchronously);
        var model = new SignInSession(new Provider(_ => finish.Task));
        var attempt = model.SignInAsync(Ct); model.CancelPendingSignIn();
        finish.SetResult(new(SignInOutcome.Succeeded, Reader())); await attempt;
        Assert.Null(model.Reader); Assert.Contains("canceled", model.StatusMessage); Assert.True(model.CanStart);
    }
    [Fact]
    public async Task Invalid_success_identity_is_rejected()
    {
        foreach (var reader in new[] { Reader(""), new SignedInReader(new Uri("https://other.auth0.com/"), "subject", "Name"), Reader(new string('x', 257)) })
        {
            var model = new SignInSession(new Provider(_ => Task.FromResult(new SignInReply(SignInOutcome.Succeeded, reader))));
            await model.SignInAsync(Ct); Assert.Null(model.Reader); Assert.True(model.CanStart);
        }
    }
    [Fact]
    public async Task A_failed_attempt_can_be_followed_by_success()
    {
        var calls = 0;
        var model = new SignInSession(new Provider(_ => Task.FromResult(++calls == 1
            ? new SignInReply(SignInOutcome.Rejected) : new(SignInOutcome.Succeeded, Reader()))));
        await model.SignInAsync(Ct); Assert.Null(model.Reader);
        await model.SignInAsync(Ct); Assert.NotNull(model.Reader); Assert.Equal(2, calls);
    }
}

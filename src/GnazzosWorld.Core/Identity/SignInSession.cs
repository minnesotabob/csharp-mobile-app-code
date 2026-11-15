using GnazzosWorld.Core.Presentation;

namespace GnazzosWorld.Core.Identity;

public enum SignInOutcome { Succeeded, Canceled, Rejected, Unconfigured }
public sealed record SignedInReader(Uri Issuer, string Subject, string DisplayName);
public sealed record SignInReply(SignInOutcome Outcome, SignedInReader? Reader = null);
public interface IInteractiveSignIn
{
    Uri? ExpectedIssuer { get; }
    Task<SignInReply> LoginAsync(CancellationToken ct);
}

/// <summary>Presentation state only. No access tokens, persisted session or API authorization.</summary>
public sealed class SignInSession(IInteractiveSignIn provider) : ObservableObject
{
    private int busy;
    private long generation;
    private CancellationTokenSource? pending;
    public SignedInReader? Reader { get; private set; }
    public bool IsBusy => Volatile.Read(ref busy) != 0;
    public bool CanStart => !IsBusy && Reader is null;
    public string DisplayName => Reader?.DisplayName ?? "Browsing anonymously";
    public string StatusMessage { get; private set; } = "Sign-in is optional for the public catalog.";
    private void Publish(string message)
    {
        StatusMessage = message;
        Notify(nameof(Reader)); Notify(nameof(DisplayName)); Notify(nameof(StatusMessage));
        Notify(nameof(IsBusy)); Notify(nameof(CanStart));
    }
    public void CancelPendingSignIn() => pending?.Cancel();
    public void ForgetLocalIdentity()
    {
        Interlocked.Increment(ref generation);
        pending?.Cancel(); Reader = null;
        Publish("Local identity cleared. The provider's browser session may still be active.");
    }
    public async Task SignInAsync(CancellationToken ct = default)
    {
        if (Reader is not null || Interlocked.CompareExchange(ref busy, 1, 0) != 0) return;
        var current = Interlocked.Read(ref generation);
        using var linked = CancellationTokenSource.CreateLinkedTokenSource(ct);
        pending = linked;
        Publish("Opening the provider's sign-in page...");
        try
        {
            linked.Token.ThrowIfCancellationRequested();
            var reply = await provider.LoginAsync(linked.Token);
            if (current != Interlocked.Read(ref generation)) return;
            linked.Token.ThrowIfCancellationRequested();
            switch (reply.Outcome)
            {
                case SignInOutcome.Succeeded:
                    var reader = reply.Reader;
                    if (reader is null || provider.ExpectedIssuer is null || reader.Issuer != provider.ExpectedIssuer ||
                        string.IsNullOrWhiteSpace(reader.Subject) || reader.Subject.Length > 256 || reader.Subject.Any(char.IsControl) ||
                        string.IsNullOrWhiteSpace(reader.DisplayName) || reader.DisplayName.Length > 120 || reader.DisplayName.Any(char.IsControl))
                    {
                        Publish("Sign-in did not return the expected identity. You can continue browsing.");
                        return;
                    }
                    Reader = reader;
                    Publish("Signed in for this process. Customer API access is not enabled in this chapter.");
                    break;
                case SignInOutcome.Canceled:
                    Publish("Sign-in canceled. You can continue browsing."); break;
                case SignInOutcome.Unconfigured:
                    Publish("Configure the native provider application using docs/labs/ch22-identity.md first."); break;
                default:
                    Publish("Sign-in could not be completed. You can continue browsing or retry."); break;
            }
        }
        catch (OperationCanceledException)
        {
            if (current == Interlocked.Read(ref generation)) Publish("Sign-in canceled. You can continue browsing.");
        }
        catch (Exception)
        {
            // Do not publish raw provider exceptions, callback URIs or token-bearing responses.
            if (current == Interlocked.Read(ref generation)) Publish("Sign-in could not be completed. You can continue browsing or retry.");
        }
        finally
        {
            pending = null;
            Interlocked.Exchange(ref busy, 0);
            Publish(StatusMessage);
        }
    }
}

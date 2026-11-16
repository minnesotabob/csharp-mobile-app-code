using System.Net.Mail;
using GnazzosWorld.Core.Commerce;

namespace GnazzosWorld.Core.Operations;

public enum NewsletterOutcome { PendingConfirmation, Rejected, Unconfirmed }
public sealed record NewsletterIntent(string OperationKey,string Email,string ConsentVersion)
{public override string ToString()=>"Newsletter intent [address omitted]";}
public interface INewsletterGateway
{
    Task<NewsletterOutcome> RequestConfirmationAsync(NewsletterIntent intent,CancellationToken ct);
}
/// <summary>Explicit consent port. No contact-draft reuse, automatic subscription or real provider is configured.</summary>
public sealed class NewsletterBoundary(INewsletterGateway gateway)
{
    public async Task<NewsletterOutcome> RequestAsync(string operationKey,string email,bool explicitConsent,CancellationToken ct)
    {
        ct.ThrowIfCancellationRequested();if(!explicitConsent)throw new InvalidOperationException("Explicit newsletter consent is required.");
        if(email is null||!OrderContract.Id(operationKey)||email.Length>254||email.Any(char.IsControl)||!MailAddress.TryCreate(email,out var parsed)||parsed.Address!=email)throw new InvalidDataException("Invalid newsletter request.");
        using var budget=CancellationTokenSource.CreateLinkedTokenSource(ct);budget.CancelAfter(TimeSpan.FromSeconds(10));
        try{var outcome=await gateway.RequestConfirmationAsync(new(operationKey,email,"newsletter-v1"),budget.Token).WaitAsync(budget.Token);budget.Token.ThrowIfCancellationRequested();return Enum.IsDefined(outcome)?outcome:NewsletterOutcome.Unconfirmed;}
        catch(OperationCanceledException) when(!ct.IsCancellationRequested){return NewsletterOutcome.Unconfirmed;}
        catch(HttpRequestException){return NewsletterOutcome.Unconfirmed;}
    }
}

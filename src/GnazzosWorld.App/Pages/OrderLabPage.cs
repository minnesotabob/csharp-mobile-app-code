using GnazzosWorld.Core.Commerce;
using GnazzosWorld.Core.Identity;
using GnazzosWorld.Core.Orders;

namespace GnazzosWorld.App.Pages;

public sealed class OrderLabPage:ContentPage
{
    private readonly ApiSession session;private readonly OrderApiClient api;private readonly PendingOrderDatabase pending;private readonly OrderAttemptCoordinator attempts;
    private readonly Label status=new(){Text="Synthetic quote/order lab. Configure API identity, order scopes and the opt-in commerce backend first."};
    private readonly Label quoteText=new();private OrderQuote? quote;private OwnedAccessLease? quoteOwner;private CancellationTokenSource? active;
    private Shell? observedShell; private Window? observedWindow; private bool attached;
    public OrderLabPage(ApiSession session,OrderApiClient api,PendingOrderDatabase pending,OrderAttemptCoordinator attempts,IRequestDraftStore drafts)
    {
        this.session=session;this.api=api;this.pending=pending;this.attempts=attempts;Title="Quote and order lab";
        var login=new Button{Text="Sign in for the configured API lab"};
        var sample=new Button{Text="Quote a synthetic sample"};var saved=new Button{Text="Send my saved synthetic draft for a quote"};
        var payment=new Button{Text="Refresh server test-payment status"};
        var accept=new Button{Text="Accept the displayed teaching quote"};var resume=new Button{Text="Retry/refresh my saved acceptance"};var signOut=new Button{Text="Sign out locally"};
        login.Clicked+=async(_,_)=>await Run(async ct=>{await session.SignInAsync(ct);quote=null;quoteOwner=null;quoteText.Text="";status.Text="Signed in. Choose a quote or reconcile this account's saved operation.";});
        sample.Clicked+=async(_,_)=>await Run(ct=>QuoteAsync(Synthetic(),ct));
        saved.Clicked+=async(_,_)=>await Run(async ct=>
        {
            var draft=drafts.Load();if(draft is null){status.Text="No saved draft exists. Save a synthetic request in the earlier form first.";return;}
            await QuoteAsync(draft,ct);
        });
        accept.Clicked+=async(_,_)=>await Run(async ct=>
        {
            if(quote is null||quoteOwner is null)throw new InvalidOperationException("Prepare a quote first.");
            var result=await attempts.AcceptAsync(quote,quoteOwner,ct);ct.ThrowIfCancellationRequested();Show(result);
        });
        resume.Clicked+=async(_,_)=>await Run(async ct=>
        { var owner=await session.AcquireOwnedAsync(ct);var result=await attempts.ResumeAsync(owner,ct);ct.ThrowIfCancellationRequested();Show(result); });
        payment.Clicked+=async(_,_)=>await Run(async ct=>
        {
            var owner=await session.AcquireOwnedAsync(ct);var operation=pending.Read(attempts.Account(owner));
            if(operation?.Confirmed is not { } order)throw new InvalidOperationException("Reconcile an accepted request first.");
            var receipt=await api.GetTestPaymentAsync(order.Id,owner,ct);ct.ThrowIfCancellationRequested();
            status.Text=$"Server TEST payment: {receipt.State}, version {receipt.Version}. This page never launches checkout or infers payment from a browser return.";
        });
        signOut.Clicked+=async(_,_)=>
        {
            active?.Cancel();quote=null;quoteOwner=null;quoteText.Text="";
            try{await session.SignOutLocalAsync();status.Text="Local API session cleared. Saved acceptances remain isolated to their original account/resource for reconciliation.";}
            catch{status.Text="Local sign-out cleanup is unconfirmed. Current use is blocked; retry sign-out.";}
        };
        Content=new ScrollView{Content=new VerticalStackLayout{Padding=24,Spacing=14,Children=
        {new Label{Text="Quotes, orders and customer ownership",FontSize=25},
         new Label{Text="Use synthetic drafts only. Quote actions send the selected story/contact fields to your configured HTTPS API. Acceptance creates a lab database request; it does not pay, charge, begin real production or upload attachments. Server prices are versioned teaching snapshots, not live offers. Local pending data is plaintext and owner/resource-partitioned."},
         login,sample,saved,quoteText,accept,resume,payment,signOut,status}}};
        async Task QuoteAsync(SongRequest draft,CancellationToken ct)
        {
            var owner=await session.AcquireOwnedAsync(ct);var existing=pending.Read(attempts.Account(owner));
            if(existing is {Confirmed:null,Rejected:null}){status.Text="An unresolved saved acceptance exists. Retry it before creating another request.";return;}
            quote=null;quoteOwner=null;quoteText.Text="";
            var result=await api.QuoteAsync(draft,owner,ct);ct.ThrowIfCancellationRequested();if(!session.IsCurrent(owner.Access))throw new SessionEndedException();
            quote=result;quoteOwner=owner;
            quoteText.Text=$"{result.Request.Minutes} minutes × {result.Request.Quantity}; {result.Request.Genre}; {result.Request.Occasion}\nVocals: {result.Request.WithVocals}; explicit allowed: {result.Request.AllowExplicit}\nVocal notes: {result.Request.VocalNotes}\nStory: {result.Request.Story}\nContact: {result.Request.ContactName} / {result.Request.Email}\nTeaching total {result.Currency} {result.TotalMinor/100m:0.00}; expires {result.ExpiresAt:u}.";
            status.Text="Review the server-normalized snapshot before accepting. Expiry is enforced by the server.";
        }
        void Show(OrderSnapshot result)
        {quote=null;quoteOwner=null;quoteText.Text="";status.Text=$"Owned request {result.Id}: {result.State}, version {result.Version}. Teaching total {result.Currency} {result.TotalMinor/100m:0.00}. No payment is asserted.";}
    }
    private async Task Run(Func<CancellationToken,Task> action)
    {
        if(active is not null)return;using var cancellation=new CancellationTokenSource();active=cancellation;
        try{await action(cancellation.Token);}
        catch(OperationCanceledException){ }
        catch(OrderApiException ex){status.Text=ex.Problem is { } p&&PendingOrderDatabase.Definitive(p)?"The server rejected this acceptance. A new quote may be requested; the prior rejection remains recorded.":"No acceptance confirmation. Keep the saved operation and retry/reconcile under the same account.";}
        catch{status.Text="Operation not confirmed. Check API/provider setup and retain any saved acceptance for retry. No paid/production outcome is reported.";}
        finally{active=null;}
    }
    protected override void OnAppearing()
    {
        base.OnAppearing();if(attached)return;attached=true;observedShell=Shell.Current;observedWindow=Window;
        if(observedShell is not null)observedShell.Navigated+=Navigated;
        if(observedWindow is not null)observedWindow.Destroying+=Destroyed;
    }
    private void Navigated(object? sender,ShellNavigatedEventArgs e){if(observedShell?.CurrentPage!=this)Leave();}
    private void Destroyed(object? sender,EventArgs e)=>Leave();
    private void Leave()
    {
        attached=false;active?.Cancel();quote=null;quoteOwner=null;quoteText.Text="";
        status.Text="Order lab left. Sign in or retry/refresh under the current account to view its saved result.";
        if(observedShell is not null)observedShell.Navigated-=Navigated;
        if(observedWindow is not null)observedWindow.Destroying-=Destroyed;
    }
    private static SongRequest Synthetic()=>new(){Genre="Folk",Occasion="Fictitious anniversary",Story="Invented story for the commerce lab only.",ContactName="Sample Reader",Email="reader@example.test"};
}

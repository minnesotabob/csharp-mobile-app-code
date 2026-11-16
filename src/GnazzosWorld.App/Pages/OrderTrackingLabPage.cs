using GnazzosWorld.Core.Commerce;
using GnazzosWorld.Core.Fulfillment;
using GnazzosWorld.Core.Identity;

namespace GnazzosWorld.App.Pages;

public sealed class OrderTrackingLabPage:ContentPage
{
    private readonly ApiSession session;private readonly TrackingApiClient api;private readonly RevisionCoordinator revisions;
    private readonly Picker orders=new(){Title="Select an owned order",ItemDisplayBinding=new Binding(nameof(Choice.Label))};
    private readonly Label detail=new(),status=new(){Text="Configure the opt-in synthetic fulfillment API and identity first."};
    private readonly Editor note=new(){Placeholder="Synthetic revision note, at most 1000 characters",MaxLength=1000,AutoSize=EditorAutoSizeOption.TextChanges};
    private OrderTracking? current;private string? next;private CancellationTokenSource? active;private bool updating,attached;private Shell? observedShell;private Window? observedWindow;
    public OrderTrackingLabPage(ApiSession session,TrackingApiClient api,RevisionCoordinator revisions)
    {
        this.session=session;this.api=api;this.revisions=revisions;Title="Order tracking lab";
        var signIn=new Button{Text="Sign in to configured API"};var first=new Button{Text="Refresh first page of my orders"};var more=new Button{Text="Next owned page"};var refresh=new Button{Text="Refresh selected history and notices"};
        var submit=new Button{Text="Save and send revision request"};var retry=new Button{Text="Retry my saved revision for this order"};var fetch=new Button{Text="Fetch and verify delivered sample audio"};var signOut=new Button{Text="Sign out locally"};
        signIn.Clicked+=async(_,_)=>await Run(async ct=>{ClearAll();await session.SignInAsync(ct);status.Text="Signed in. Explicitly refresh owned orders.";});
        first.Clicked+=async(_,_)=>await Run(ct=>LoadPage(null,ct));
        more.Clicked+=async(_,_)=>await Run(ct=>next is null?Task.CompletedTask:LoadPage(next,ct));
        refresh.Clicked+=async(_,_)=>await Run(async ct=>{var id=Selected();ClearDetail();var owner=await session.AcquireOwnedAsync(ct);var result=await api.TrackAsync(id,owner,ct);Current(owner,ct);current=result;detail.Text=Describe(result);status.Text="Owned snapshot loaded. Notices are durable in-app records, not push delivery receipts.";});
        submit.Clicked+=async(_,_)=>await Run(async ct=>
        {
            var snapshot=current??throw new InvalidOperationException("Refresh selected tracking first.");var text=note.Text??"";var owner=await session.AcquireOwnedAsync(ct);
            var receipt=await revisions.SubmitAsync(snapshot.Order.Id,snapshot.Order.Version,text,owner,ct);Current(owner,ct);ClearDetail();note.Text="";
            status.Text=$"Revision confirmed at version {receipt.AppliedVersion}. Refresh history before another action.";
        });
        retry.Clicked+=async(_,_)=>await Run(async ct=>{var id=Selected();var owner=await session.AcquireOwnedAsync(ct);var receipt=await revisions.ResumeAsync(id,owner,ct);Current(owner,ct);ClearDetail();note.Text="";status.Text=$"Saved revision confirmed at version {receipt.AppliedVersion}. Refresh history.";});
        fetch.Clicked+=async(_,_)=>await Run(async ct=>
        {
            var snapshot=current??throw new InvalidOperationException("Refresh tracking first.");var delivery=snapshot.Deliveries.SingleOrDefault(d=>!d.Revoked)??throw new InvalidOperationException("No active delivery.");
            var owner=await session.AcquireOwnedAsync(ct);using var verified=await api.FetchAsync(snapshot.Order.Id,delivery,snapshot.Order.Version,owner,ct);Current(owner,ct);
            status.Text=$"Verified {verified.Bytes.Length} bytes of the original three-second WAV sample. Memory-only access; no playback, export or offline cache. Buffer cleared on completion.";
        });
        signOut.Clicked+=async(_,_)=>{active?.Cancel();ClearAll();try{await session.SignOutLocalAsync();status.Text="Local session cleared. Original-account pending revisions remain for explicit reconciliation.";}catch{status.Text="Sign-out cleanup is unconfirmed; current use is blocked.";}};
        orders.SelectedIndexChanged+=(_,_)=>{if(updating)return;active?.Cancel();ClearDetail();note.Text="";status.Text="Refresh the selected order. Earlier list status may be stale.";};
        Content=new ScrollView{Content=new VerticalStackLayout{Padding=24,Spacing=12,Children={new Label{Text="Tracking, revisions and private delivery",FontSize=25},new Label{Text="Synthetic Development lab only. No real production or customer upload. Submitted revisions are saved before HTTP under the original account/resource/order; unsent editor text is cleared on departure. Audio is fetched into bounded memory, verified and cleared. Actual listening, OS copies and provider push are separate work."},signIn,first,more,orders,refresh,detail,note,submit,retry,fetch,signOut,status}}};
    }
    private string Selected()=>(orders.SelectedItem as Choice)?.Id??throw new InvalidOperationException("Select an owned order.");
    private void Current(OwnedAccessLease owner,CancellationToken ct){ct.ThrowIfCancellationRequested();_=api.Account(owner);}
    private async Task LoadPage(string? after,CancellationToken ct)
    {var owner=await session.AcquireOwnedAsync(ct);var page=await api.ListAsync(after,owner,ct);Current(owner,ct);ClearDetail();note.Text="";updating=true;try{orders.ItemsSource=page.Items.Select(x=>new Choice(x.Id,$"{x.Id[..8]} · {x.State} · v{x.Version}")).ToArray();orders.SelectedIndex=-1;next=page.Next;}finally{updating=false;}status.Text="Live owned page loaded (up to 20). New inserts may require refreshing the first page.";}
    private static string Describe(OrderTracking t)=>$"{t.Order.State}; version {t.Order.Version}\nHistory:\n"+string.Join("\n",t.History.Select(h=>$"v{h.Version}: {h.State}"))+"\nRevisions:\n"+string.Join("\n",t.Revisions.Select(r=>$"v{r.AppliedVersion}: {r.Note}"))+"\nNotices:\n"+string.Join("\n",t.Notices.Select(n=>$"v{n.Version}: {n.Kind}"))+"\nDeliveries: "+string.Join(", ",t.Deliveries.Select(d=>d.Revoked?"Revoked":"Available (recheck required)"));
    private void ClearDetail(){current=null;detail.Text="";}
    private void ClearAll(){ClearDetail();note.Text="";next=null;updating=true;try{orders.ItemsSource=null;orders.SelectedIndex=-1;}finally{updating=false;}}
    private async Task Run(Func<CancellationToken,Task> action)
    {if(active is not null)return;using var cts=new CancellationTokenSource();active=cts;try{await action(cts.Token);}catch(OperationCanceledException){}catch{status.Text="Operation unconfirmed. Retain saved revisions; retry under the same account or refresh current history/grants. Check API setup.";}finally{active=null;}}
    protected override void OnAppearing(){base.OnAppearing();if(attached)return;attached=true;observedShell=Shell.Current;observedWindow=Window;if(observedShell is not null)observedShell.Navigated+=Navigated;if(observedWindow is not null)observedWindow.Destroying+=Destroyed;}
    private void Navigated(object? sender,ShellNavigatedEventArgs e){if(observedShell?.CurrentPage!=this)Leave();}
    private void Destroyed(object? sender,EventArgs e)=>Leave();
    private void Leave(){attached=false;active?.Cancel();ClearAll();status.Text="Tracking view cleared. Refresh under the current account to continue.";if(observedShell is not null)observedShell.Navigated-=Navigated;if(observedWindow is not null)observedWindow.Destroying-=Destroyed;}
    private sealed record Choice(string Id,string Label);
}

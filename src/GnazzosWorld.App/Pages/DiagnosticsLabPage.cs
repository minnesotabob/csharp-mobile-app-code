using GnazzosWorld.Core.Operations;

namespace GnazzosWorld.App.Pages;

public sealed class DiagnosticsLabPage:ContentPage
{
    private readonly SafeDiagnosticBuffer diagnostics;private readonly Label records=new();
    public DiagnosticsLabPage(SafeDiagnosticBuffer diagnostics)
    {
        this.diagnostics=diagnostics;Title="Diagnostics consent lab";
        var consent=new Switch{IsToggled=diagnostics.Enabled};var simulate=new Button{Text="Record a simulated recoverable storage failure"};
        consent.Toggled+=(_,e)=>{diagnostics.SetConsent(e.Value);Preferences.Default.Set("safe-diagnostic-consent-v1",e.Value);Render();};
        simulate.Clicked+=(_,_)=>
        {
            try{throw new IOException("SYNTHETIC_PRIVATE_EXCEPTION_CANARY");}
            catch(IOException){diagnostics.Record(DiagnosticArea.Storage,DiagnosticFault.Unexpected);}
            Render();
        };
        Content=new ScrollView{Content=new VerticalStackLayout{Padding=24,Spacing=14,Children={new Label{Text="Local diagnostic consent",FontSize=25},new Label{Text="Synthetic lab only. Collection defaults off and remains on this installation; no crash SDK or network exporter is configured. The last 20 typed events contain no exception text, customer fields, credential or device identifier. Turning collection off clears the buffer. This is not a native/unhandled-crash test or physical-erasure guarantee."},new Label{Text="Allow this local diagnostic buffer"},consent,simulate,records}}};Render();
    }
    private void Render()=>records.Text=diagnostics.Enabled?"Local bounded events:\n"+string.Join("\n",diagnostics.Snapshot().Select(e=>$"{e.At:u} · {e.Area} · {e.Fault}")):"Local diagnostic collection is off. No events retained.";
    protected override void OnAppearing(){base.OnAppearing();Render();}
    protected override void OnDisappearing(){records.Text="";base.OnDisappearing();}
}

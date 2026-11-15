namespace GnazzosWorld.App;

public partial class App : Application
{
    private readonly MainPage page;
    public App(MainPage page)
    {
        InitializeComponent();
        this.page = page;
    }
    protected override Window CreateWindow(IActivationState? activationState)
        => new(new NavigationPage(page));
}

using GnazzosWorld.Core.Identity;

namespace GnazzosWorld.App.Pages;

public sealed class AccountPage : ContentPage
{
    private readonly SignInSession session;
    public AccountPage(SignInSession session)
    {
        this.session = session;
        Title = "Account";
        BindingContext = session;
        var title = new Label { Text = "Your Gnazzo's World account", FontSize = 26 };
        SemanticProperties.SetHeadingLevel(title, SemanticHeadingLevel.Level1);
        var name = new Label { FontSize = 20 }; name.SetBinding(Label.TextProperty, nameof(SignInSession.DisplayName));
        var status = new Label(); status.SetBinding(Label.TextProperty, nameof(SignInSession.StatusMessage));
        var login = new Button { Text = "Sign in with provider" };
        login.SetBinding(IsEnabledProperty, nameof(SignInSession.CanStart));
        login.Clicked += async (_, _) => await session.SignInAsync();
        var cancel = new Button { Text = "Cancel pending sign-in" };
        cancel.SetBinding(IsEnabledProperty, nameof(SignInSession.IsBusy));
        cancel.Clicked += (_, _) => session.CancelPendingSignIn();
        var forget = new Button { Text = "Forget local sign-in" };
        forget.Clicked += (_, _) => session.ForgetLocalIdentity();
        Content = new ScrollView { Content = new VerticalStackLayout
        {
            Padding = 24, Spacing = 16,
            Children = { title, name,
                new Label { Text = "Browsing stays public. This chapter does not enable customer APIs, synchronized accounts or persistent sessions." },
                status, login, cancel, forget }
        }};
    }
    protected override void OnDisappearing()
    {
        session.CancelPendingSignIn();
        base.OnDisappearing();
    }
}

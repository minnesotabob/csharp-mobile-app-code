using GnazzosWorld.Core.Presentation;

namespace GnazzosWorld.App.Pages;

public partial class DetailsPage : ContentPage, IQueryAttributable
{
    private readonly DetailViewModel viewModel;
    public DetailsPage(DetailViewModel viewModel)
    {
        InitializeComponent();
        this.viewModel = viewModel;
        BindingContext = viewModel;
    }

    public void ApplyQueryAttributes(IDictionary<string, object> query)
        => viewModel.Load(RoutePayload.ReadItemId(query));
}

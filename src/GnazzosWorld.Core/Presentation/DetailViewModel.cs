using System.Globalization;
using GnazzosWorld.Core.Catalog;
using GnazzosWorld.Core.Localization;

namespace GnazzosWorld.Core.Presentation;

public sealed class DetailViewModel : ObservableObject
{
    private readonly IReadOnlyList<CatalogItem> catalog;
    private readonly IExternalBrowser? browser;
    private int revision;
    public DetailViewModel(IReadOnlyList<CatalogItem> catalog, IExternalBrowser? browser = null)
    {
        this.catalog = catalog;
        this.browser = browser;
        OpenExternal = new AsyncCommand(OpenAsync, () => HasExternalLink && browser is not null,
            _ => { }); // OpenAsync owns errors together with its captured revision.
    }
    public CatalogItem? Item { get; private set; }
    public bool HasItem => Item is not null;
    public string Title => Item?.Title ?? UiText.Details;
    public string Creator => Item?.Creator ?? "";
    public string Description => Item?.Description ?? "";
    public string Kind => Item is null ? "" : UiText.Get(Item.Kind.ToString());
    public string Edition => Item?.Edition ?? "";
    public string ReleaseDate => Item?.ReleaseDate?.ToString("d", CultureInfo.CurrentCulture) ?? "";
    public string Duration => Item?.DurationSeconds is int seconds
        ? TimeSpan.FromSeconds(seconds).ToString(@"hh\:mm\:ss") : "";
    public string Advisory => Item is null || Item.Kind == ContentKind.Book
        ? "" : UiText.Get("Advisory" + Item.Advisory);
    public string Availability => Item is null ? "" : UiText.Get("Availability" + Item.Availability);
    public bool HasSample => Item is { Kind: ContentKind.Book,
        Availability: ContentAvailability.Listed, Sample: not null };
    public string SampleHeading => HasSample ? Item!.Sample!.Heading : "";
    public string SampleText => HasSample ? Item!.Sample!.Text : "";
    public string SampleCredit => HasSample ? Item!.Sample!.Credit : "";
    public bool HasExternalLink => Item is { Availability: ContentAvailability.Listed }
        && ContentLinks.IsAllowed(Item.ExternalUrl);
    public string ExternalHost => HasExternalLink ? Item!.ExternalUrl!.IdnHost : "";
    public string LinkMessage { get; private set; } = "";
    public string Message { get; private set; } = UiText.Choose;
    public AsyncCommand OpenExternal { get; }

    private async Task OpenAsync()
    {
        var own = revision;
        var url = Item!.ExternalUrl!;
        LinkMessage = ""; Notify(nameof(LinkMessage));
        string result;
        try { result = await browser!.OpenAsync(url) ? UiText.BrowserHandedOff : UiText.LinkFailed; }
        catch (OperationCanceledException) { result = UiText.LinkCanceled; }
        catch (Exception) { result = UiText.LinkFailed; }
        if (own != revision) return;
        LinkMessage = result; Notify(nameof(LinkMessage));
    }

    public void Load(string? id)
    {
        Item = RoutePayload.IsValidId(id)
            ? catalog.FirstOrDefault(x => x.Id == id) : null;
        Message = Item is null ? UiText.NotFound : "";
        ResetActions();
    }
    public void Clear()
    {
        Item = null;
        Message = UiText.Choose;
        ResetActions();
    }
    private void ResetActions()
    {
        revision++;
        LinkMessage = "";
        Notify(null);
        OpenExternal.Refresh();
    }
}

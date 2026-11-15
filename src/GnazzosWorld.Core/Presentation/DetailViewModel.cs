using GnazzosWorld.Core.Catalog;
using GnazzosWorld.Core.Localization;

namespace GnazzosWorld.Core.Presentation;

public sealed class DetailViewModel(IReadOnlyList<CatalogItem> catalog)
    : ObservableObject
{
    public CatalogItem? Item { get; private set; }
    public bool HasItem => Item is not null;
    public string Title => Item?.Title ?? UiText.Details;
    public string Creator => Item?.Creator ?? "";
    public string Description => Item?.Description ?? "";
    public string Message { get; private set; } = UiText.Choose;

    public void Load(string? id)
    {
        Item = RoutePayload.IsValidId(id)
            ? catalog.FirstOrDefault(x => x.Id == id) : null;
        Message = Item is null ? UiText.NotFound : "";
        Notify(null); // Every derived presentation value may have changed.
    }

    public void Clear()
    {
        Item = null;
        Message = UiText.Choose;
        Notify(null);
    }
}

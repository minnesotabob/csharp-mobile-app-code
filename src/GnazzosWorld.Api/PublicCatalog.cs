using GnazzosWorld.Core.Catalog;
using GnazzosWorld.Core.Contracts;

namespace GnazzosWorld.Api;

public interface IPublicCatalog { CatalogDocument Document { get; } }

public sealed class PublicCatalog : IPublicCatalog
{
    public CatalogDocument Document { get; }
    public PublicCatalog(CatalogSnapshot snapshot, string revision)
    {
        var document = new CatalogDocument(1, revision,
            snapshot.Items.OrderBy(i => i.Id, StringComparer.Ordinal).ToList().AsReadOnly(),
            snapshot.Creators.OrderBy(c => c.Id, StringComparer.Ordinal).ToList().AsReadOnly());
        document.Validate(); Document = document;
    }
}

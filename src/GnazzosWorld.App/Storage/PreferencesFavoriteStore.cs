using GnazzosWorld.Core.Catalog;
using Microsoft.Maui.Storage;

namespace GnazzosWorld.App.Storage;

public sealed class PreferencesFavoriteStore : IFavoriteStore
{
    private const string Key = "catalog-favorites-v1";
    public IReadOnlyList<string> Load()
        => FavoriteIdCodec.Decode(Preferences.Default.Get(Key, "[]"));
    public void Save(IReadOnlyList<string> ids)
        => Preferences.Default.Set(Key, FavoriteIdCodec.Encode(ids));
}

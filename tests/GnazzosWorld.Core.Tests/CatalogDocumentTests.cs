using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using GnazzosWorld.Core.Catalog;
using GnazzosWorld.Core.Contracts;
using Xunit;

namespace GnazzosWorld.Core.Tests;

public sealed class CatalogDocumentTests
{
    private static CatalogDocument Valid()
    {
        var snapshot = CatalogSnapshot.LoadBundled(); return new(1, "fixture-v1", snapshot.Items, snapshot.Creators);
    }
    [Theory]
    [InlineData(0)] [InlineData(2)]
    public void Unsupported_schema_is_rejected(int version)
        => Assert.Throws<InvalidDataException>(() => (Valid() with { SchemaVersion = version }).Validate());
    [Theory]
    [InlineData("")] [InlineData("bad revision")] [InlineData("https://invalid")]
    public void Malformed_revision_is_rejected(string revision)
        => Assert.Throws<InvalidDataException>(() => (Valid() with { Revision = revision }).Validate());
    [Fact]
    public void Missing_creator_reference_is_rejected()
        => Assert.Throws<InvalidDataException>(() => (Valid() with { Creators = [] }).Validate());
    [Fact]
    public void Null_collection_is_rejected()
        => Assert.Throws<InvalidDataException>(() => (Valid() with { Items = null! }).Validate());
    [Fact]
    public void Oversized_record_collection_is_rejected()
        => Assert.Throws<InvalidDataException>(() => (Valid() with { Items = Enumerable.Repeat(Valid().Items[0], 1001).ToArray() }).Validate());
    [Fact]
    public void Wire_round_trip_preserves_owned_samples_and_relationships()
    {
        var options = new JsonSerializerOptions(JsonSerializerDefaults.Web);
        options.Converters.Add(new JsonStringEnumConverter());
        var original = Valid(); using var json = new MemoryStream(Encoding.UTF8.GetBytes(JsonSerializer.Serialize(original, options)));
        var loaded = CatalogDocument.Read(json);
        Assert.Equal(original.Items, loaded.Items); Assert.Equal(original.Creators, loaded.Creators);
    }
    [Fact]
    public void Create_copies_collection_ownership()
    {
        var document = Valid(); var items = document.Items.ToList(); var creators = document.Creators.ToList();
        var snapshot = CatalogSnapshot.Create(items, creators); items.Clear(); creators.Clear();
        Assert.Equal(document.Items.Count, snapshot.Items.Count); Assert.Equal(document.Creators.Count, snapshot.Creators.Count);
    }
}

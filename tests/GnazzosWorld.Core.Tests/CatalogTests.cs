using System.Text;
using GnazzosWorld.Core.Catalog;
using Xunit;

namespace GnazzosWorld.Core.Tests;

public sealed class CatalogTests
{
    [Fact]
    public void Bundled_catalog_has_unique_ids_and_all_three_content_kinds()
    {
        var items = CatalogLoader.LoadBundled();
        Assert.Equal(5, items.Count);
        Assert.Equal(items.Count, items.Select(x => x.Id).Distinct().Count());
        Assert.Equal(3, items.Select(x => x.Kind).Distinct().Count());
    }

    [Theory]
    [InlineData(" beret VIOLET ", "song-minnesota-bob-beret-violet")]
    [InlineData("KAI", "book-kai-01")]
    public void Search_handles_case_whitespace_and_accents(string term, string id)
        => Assert.Equal(id, Assert.Single(
            CatalogSearch.Find(CatalogLoader.LoadBundled(), term)).Id);

    [Fact]
    public void Search_combines_creator_query_and_content_filter()
    {
        Assert.Equal(2, CatalogSearch.Find(CatalogLoader.LoadBundled(),
            "Minnesota Bob", ContentKind.Song).Count);
        Assert.Empty(CatalogSearch.Find(CatalogLoader.LoadBundled(),
            "Minnesota Bob", ContentKind.Book));
    }

    [Fact]
    public void Blank_search_returns_all_and_unknown_search_returns_none()
    {
        var catalog = CatalogLoader.LoadBundled();
        Assert.Equal(catalog.Count, CatalogSearch.Find(catalog, null).Count);
        Assert.Empty(CatalogSearch.Find(catalog, "no matching content"));
    }

    [Fact]
    public void Equal_titles_are_ordered_by_stable_id()
    {
        var b = new CatalogItem("b", "Same", "Creator", ContentKind.Book, "Description");
        var a = b with { Id = "a" };
        Assert.Equal(new[] { "a", "b" },
            CatalogSearch.Find(new[] { b, a }, "").Select(x => x.Id));
    }

    [Theory]
    [InlineData("[null]")]
    [InlineData("null")]
    [InlineData("[{\"id\":\"x\",\"title\":\" \",\"creator\":\"C\",\"description\":\"D\",\"kind\":\"Book\"}]")]
    [InlineData("[{\"id\":\"x\",\"title\":\"T\",\"creator\":\"C\",\"description\":\"D\",\"kind\":99}]")]
    [InlineData("[{\"id\":\"x\",\"title\":\"T\",\"creator\":\"C\",\"description\":\"D\",\"kind\":\"Book\",\"externalUrl\":\"http://example.com\"}]")]
    public void Invalid_metadata_is_rejected(string json)
    {
        using var stream = new MemoryStream(Encoding.UTF8.GetBytes(json));
        Assert.Throws<InvalidDataException>(() => CatalogLoader.Load(stream));
    }

    [Fact]
    public void Duplicate_ids_are_rejected_even_when_titles_differ()
    {
        var json = """
            [{"id":"same","title":"A","creator":"C","kind":"Book","description":"D"},
             {"id":"same","title":"B","creator":"C","kind":"Song","description":"D"}]
            """;
        using var stream = new MemoryStream(Encoding.UTF8.GetBytes(json));
        Assert.Throws<InvalidDataException>(() => CatalogLoader.Load(stream));
    }

    [Fact]
    public void Missing_content_kind_is_rejected_instead_of_defaulting_to_book()
    {
        const string json = """
            [{"id":"x","title":"Title","creator":"Creator","description":"D"}]
            """;
        using var stream = new MemoryStream(Encoding.UTF8.GetBytes(json));
        Assert.Throws<System.Text.Json.JsonException>(() => CatalogLoader.Load(stream));
    }

    [Fact]
    public void Malformed_json_is_not_treated_as_an_empty_catalog()
    {
        using var stream = new MemoryStream(Encoding.UTF8.GetBytes("[{"));
        Assert.Throws<System.Text.Json.JsonException>(() => CatalogLoader.Load(stream));
    }

    [Fact]
    public void Valid_empty_catalog_is_allowed()
    {
        using var stream = new MemoryStream(Encoding.UTF8.GetBytes("[]"));
        Assert.Empty(CatalogLoader.Load(stream));
    }

    [Fact]
    public void Loader_does_not_take_ownership_of_the_callers_stream()
    {
        using var stream = new MemoryStream(Encoding.UTF8.GetBytes("[]"));
        CatalogLoader.Load(stream);
        Assert.True(stream.CanRead);
    }

    [Fact]
    public void Null_input_stream_is_rejected()
        => Assert.Throws<ArgumentNullException>(() => CatalogLoader.Load(null!));
}

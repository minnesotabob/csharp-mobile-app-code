using System.Text;
using System.Text.Json;
using GnazzosWorld.Core.Catalog;
using Xunit;

namespace GnazzosWorld.Core.Tests;

public sealed class CatalogMetadataTests
{
    private const string Minimal =
        """[{"id":"x","title":"Title","creator":"Artist","kind":"Song","description":"D"}]""";
    private const string Creators =
        """[{"id":"artist","name":"Artist","kind":"Artist"}]""";

    private static MemoryStream StreamOf(string json)
        => new(Encoding.UTF8.GetBytes(json));

    private static IReadOnlyList<CatalogItem> LoadWith(string extra)
    {
        using var stream = StreamOf(Minimal.Replace("\"description\":\"D\"",
            "\"description\":\"D\"," + extra));
        return CatalogLoader.Load(stream);
    }

    [Fact]
    public void Legacy_minimal_records_keep_unknown_metadata()
    {
        using var stream = StreamOf(Minimal);
        var item = Assert.Single(CatalogLoader.Load(stream));
        Assert.Null(item.CreatorId);
        Assert.Null(item.ReleaseDate);
        Assert.Null(item.DurationSeconds);
        Assert.Equal(ContentAvailability.Unknown, item.Availability);
        Assert.Equal(ContentAdvisory.Unknown, item.Advisory);
    }

    [Fact]
    public void Valid_metadata_is_preserved_by_the_import_boundary()
    {
        var item = Assert.Single(LoadWith(
            "\"releaseDate\":\"2026-01-15\",\"durationSeconds\":180," +
            "\"availability\":\"Listed\",\"advisory\":\"Clean\""));
        Assert.Equal(new DateOnly(2026, 1, 15), item.ReleaseDate);
        Assert.Equal(180, item.DurationSeconds);
        Assert.Equal(ContentAvailability.Listed, item.Availability);
        Assert.Equal(ContentAdvisory.Clean, item.Advisory);
    }

    [Theory]
    [InlineData("\"durationSeconds\":0")]
    [InlineData("\"durationSeconds\":-1")]
    [InlineData("\"creatorId\":\" \"")]
    [InlineData("\"edition\":\"Second\"")]
    [InlineData("\"availability\":99")]
    [InlineData("\"advisory\":99")]
    public void Invalid_optional_metadata_is_rejected(string extra)
        => Assert.Throws<InvalidDataException>(() => LoadWith(extra));

    [Theory]
    [InlineData("\"releaseDate\":\"not-a-date\"")]
    [InlineData("\"releaseDate\":\"2026-02-30\"")]
    [InlineData("\"availability\":\"MissingValue\"")]
    [InlineData("\"advisory\":\"MissingValue\"")]
    public void Malformed_metadata_is_not_silently_defaulted(string extra)
        => Assert.Throws<JsonException>(() => LoadWith(extra));

    [Fact]
    public void Book_editions_are_supported_but_audio_duration_is_rejected()
    {
        using var valid = StreamOf(Minimal.Replace("Song", "Book")
            .Replace("\"description\":\"D\"", "\"description\":\"D\",\"edition\":\"Second\""));
        Assert.Equal("Second", Assert.Single(CatalogLoader.Load(valid)).Edition);
        using var invalid = StreamOf(Minimal.Replace("Song", "Book")
            .Replace("\"description\":\"D\"", "\"description\":\"D\",\"durationSeconds\":60"));
        Assert.Throws<InvalidDataException>(() => CatalogLoader.Load(invalid));
    }

    [Fact]
    public void Snapshot_resolves_stable_creator_ids_and_keeps_streams_open()
    {
        using var items = StreamOf(Minimal.Replace("\"description\":\"D\"",
            "\"description\":\"D\",\"creatorId\":\"artist\""));
        using var creators = StreamOf(Creators);
        var snapshot = CatalogSnapshot.Load(items, creators);
        Assert.Equal(Assert.Single(snapshot.Creators).Id,
            Assert.Single(snapshot.Items).CreatorId);
        Assert.True(items.CanRead);
        Assert.True(creators.CanRead);
    }

    [Fact]
    public void Dangling_creator_reference_rejects_the_whole_snapshot()
    {
        using var items = StreamOf(Minimal.Replace("\"description\":\"D\"",
            "\"description\":\"D\",\"creatorId\":\"missing\""));
        using var creators = StreamOf(Creators);
        Assert.Throws<InvalidDataException>(() => CatalogSnapshot.Load(items, creators));
        Assert.True(items.CanRead);
        Assert.True(creators.CanRead);
    }

    [Theory]
    [InlineData("null")]
    [InlineData("[null]")]
    [InlineData("[{\"id\":\"artist\",\"name\":\" \",\"kind\":\"Artist\"}]")]
    [InlineData("[{\"id\":\"artist\",\"name\":\"A\",\"kind\":99}]")]
    [InlineData("[{\"id\":\"same\",\"name\":\"A\",\"kind\":\"Artist\"},{\"id\":\"same\",\"name\":\"B\",\"kind\":\"Host\"}]")]
    public void Invalid_creator_sets_are_rejected(string json)
    {
        using var items = StreamOf(Minimal);
        using var creators = StreamOf(json);
        Assert.Throws<InvalidDataException>(() => CatalogSnapshot.Load(items, creators));
    }

    [Fact]
    public void Bundled_snapshot_has_resolvable_creators_without_invented_release_dates()
    {
        var snapshot = CatalogSnapshot.LoadBundled();
        Assert.Equal(3, snapshot.Creators.Count);
        Assert.All(snapshot.Items, item =>
        {
            Assert.Contains(snapshot.Creators, creator => creator.Id == item.CreatorId);
            Assert.Null(item.ReleaseDate);
            Assert.Equal(ContentAdvisory.Unknown, item.Advisory);
        });
        Assert.Equal(ContentAvailability.Unavailable,
            Assert.Single(snapshot.Items, x => x.Kind == ContentKind.Podcast).Availability);
    }

    [Fact]
    public void Renaming_display_text_preserves_identity()
    {
        var creator = new CatalogCreator("artist", "Old name", CreatorKind.Artist);
        Assert.Equal(creator.Id, (creator with { Name = "New name" }).Id);
    }
}

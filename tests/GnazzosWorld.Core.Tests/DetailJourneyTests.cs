using System.Text;
using GnazzosWorld.Core.Catalog;
using GnazzosWorld.Core.Localization;
using GnazzosWorld.Core.Presentation;
using Xunit;

namespace GnazzosWorld.Core.Tests;

public sealed class DetailJourneyTests
{
    private sealed class Browser(Func<Uri, Task<bool>> action) : IExternalBrowser
    {
        public List<Uri> Opened { get; } = [];
        public Task<bool> OpenAsync(Uri uri) { Opened.Add(uri); return action(uri); }
    }
    private static CatalogItem Book => new("book", "Title", "Author", ContentKind.Book,
        "Description", new Uri("https://johngnazzo.com/books.html"),
        Availability: ContentAvailability.Listed,
        Sample: new("Sample", "First paragraph.\n\nSecond paragraph.", "Teaching fixture"));

    [Theory]
    [InlineData("https://johngnazzo.com/books.html", true)]
    [InlineData("https://WWW.JOHNGNAZZO.COM/books.html", true)]
    [InlineData("http://johngnazzo.com/books.html", false)]
    [InlineData("https://johngnazzo.com.evil.test/", false)]
    [InlineData("https://evil.test/johngnazzo.com", false)]
    [InlineData("https://user@johngnazzo.com/", false)]
    [InlineData("https://johngnazzo.com:444/", false)]
    [InlineData("file:///tmp/sample", false)]
    [InlineData("javascript:alert(1)", false)]
    [InlineData("/books.html", false)]
    public void Only_reviewed_https_destinations_are_allowed(string url, bool expected)
        => Assert.Equal(expected, ContentLinks.IsAllowed(new Uri(url, UriKind.RelativeOrAbsolute)));

    [Fact]
    public void Missing_and_unknown_records_clear_all_derived_content()
    {
        var vm = new DetailViewModel([Book]); vm.Load("book");
        Assert.True(vm.HasSample); Assert.Contains("\n\n", vm.SampleText);
        vm.Load("missing");
        Assert.False(vm.HasItem); Assert.False(vm.HasSample); Assert.False(vm.HasExternalLink);
        Assert.Empty(vm.SampleText); Assert.Empty(vm.ExternalHost); Assert.Empty(vm.Edition);
        Assert.False(vm.OpenExternal.CanExecute(null));
    }

    [Fact]
    public async Task Unavailable_content_keeps_metadata_but_blocks_sample_and_link()
    {
        var browser = new Browser(_ => Task.FromResult(true));
        var vm = new DetailViewModel([Book with { Availability = ContentAvailability.Unavailable }], browser);
        vm.Load("book"); await vm.OpenExternal.ExecuteAsync();
        Assert.Equal("Title", vm.Title); Assert.False(vm.HasSample);
        Assert.False(vm.HasExternalLink); Assert.Empty(browser.Opened);
    }

    [Fact]
    public void Audio_metadata_does_not_create_playback_or_a_book_sample()
    {
        var vm = new DetailViewModel([Book with { Kind = ContentKind.Song, Sample = null,
            DurationSeconds = 185, Advisory = ContentAdvisory.Explicit }]);
        vm.Load("book"); Assert.Equal("00:03:05", vm.Duration);
        Assert.False(vm.HasSample); Assert.NotEmpty(vm.Advisory); Assert.Empty(vm.ReleaseDate);
    }

    [Theory]
    [InlineData(true)] [InlineData(false)]
    public async Task Browser_result_means_handoff_or_retryable_failure(bool result)
    {
        var browser = new Browser(_ => Task.FromResult(result));
        var vm = new DetailViewModel([Book], browser); vm.Load("book");
        await vm.OpenExternal.ExecuteAsync();
        Assert.Equal(Book.ExternalUrl, Assert.Single(browser.Opened));
        Assert.Equal(result ? UiText.BrowserHandedOff : UiText.LinkFailed, vm.LinkMessage);
        Assert.True(vm.OpenExternal.CanExecute(null));
    }

    [Theory]
    [InlineData(true)] [InlineData(false)]
    public async Task Canceled_and_failed_openings_can_be_retried(bool canceled)
    {
        var calls = 0;
        var browser = new Browser(_ => ++calls == 1
            ? Task.FromException<bool>(canceled ? new OperationCanceledException() : new IOException())
            : Task.FromResult(true));
        var vm = new DetailViewModel([Book], browser); vm.Load("book");
        await vm.OpenExternal.ExecuteAsync();
        Assert.Equal(canceled ? UiText.LinkCanceled : UiText.LinkFailed, vm.LinkMessage);
        await vm.OpenExternal.ExecuteAsync(); Assert.Equal(UiText.BrowserHandedOff, vm.LinkMessage);
    }

    [Theory]
    [InlineData(true)] [InlineData(false)]
    public async Task Late_result_or_exception_cannot_replace_a_new_records_message(bool fail)
    {
        var pending = new TaskCompletionSource<bool>();
        var browser = new Browser(_ => pending.Task);
        var vm = new DetailViewModel([Book, Book with { Id = "other", Title = "Other" }], browser);
        vm.Load("book"); var first = vm.OpenExternal.ExecuteAsync();
        await vm.OpenExternal.ExecuteAsync(); Assert.Single(browser.Opened);
        vm.Load("other");
        if (fail) pending.SetException(new IOException()); else pending.SetResult(true);
        await first;
        Assert.Equal("Other", vm.Title); Assert.Empty(vm.LinkMessage);
        Assert.True(vm.OpenExternal.CanExecute(null));
    }

    [Fact]
    public async Task Clear_invalidates_pending_completion()
    {
        var pending = new TaskCompletionSource<bool>();
        var vm = new DetailViewModel([Book], new Browser(_ => pending.Task));
        vm.Load("book"); var opening = vm.OpenExternal.ExecuteAsync(); vm.Clear();
        pending.SetResult(false); await opening;
        Assert.Empty(vm.LinkMessage); Assert.False(vm.HasItem);
    }

    [Theory]
    [InlineData("Song", "text", "credit")]
    [InlineData("Book", "", "credit")]
    [InlineData("Book", "text", "")]
    public void Import_rejects_invalid_samples(string kind, string text, string credit)
    {
        var json = System.Text.Json.JsonSerializer.Serialize(new[] {
            new { id = "book", title = "T", creator = "A", description = "D", kind,
                sample = new { heading = "Sample", text, credit } } });
        using var stream = new MemoryStream(Encoding.UTF8.GetBytes(json));
        Assert.Throws<InvalidDataException>(() => CatalogLoader.Load(stream));
    }

    [Fact]
    public void Metadata_remains_available_without_a_native_adapter()
    {
        var vm = new DetailViewModel([Book]); vm.Load("book");
        Assert.True(vm.HasItem); Assert.True(vm.HasSample);
        Assert.False(vm.OpenExternal.CanExecute(null));
    }

    [Fact]
    public async Task Unreviewed_host_never_reaches_browser()
    {
        var browser = new Browser(_ => Task.FromResult(true));
        var vm = new DetailViewModel([Book with { ExternalUrl = new Uri("https://example.org/") }], browser);
        vm.Load("book"); await vm.OpenExternal.ExecuteAsync();
        Assert.False(vm.HasExternalLink); Assert.Empty(browser.Opened);
    }

    [Fact]
    public void Oversized_sample_is_rejected_during_import()
    {
        var json = System.Text.Json.JsonSerializer.Serialize(new[] {
            Book with { Sample = new("Sample", new string('x', 20001), "Credit") } });
        using var stream = new MemoryStream(Encoding.UTF8.GetBytes(json));
        Assert.Throws<InvalidDataException>(() => CatalogLoader.Load(stream));
    }

    [Fact]
    public void Bundled_sample_has_explicit_fixture_provenance()
    {
        var item = Assert.Single(CatalogLoader.LoadBundled(), x => x.Sample is not null);
        Assert.Equal(ContentKind.Book, item.Kind);
        Assert.Contains("Not an excerpt", item.Sample!.Credit);
    }
}

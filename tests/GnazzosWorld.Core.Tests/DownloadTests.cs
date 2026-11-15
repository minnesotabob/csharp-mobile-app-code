using System.Net;
using System.Net.Http.Headers;
using System.Security.Cryptography;
using GnazzosWorld.Core.Downloads;
using Xunit;

namespace GnazzosWorld.Core.Tests;

public sealed class DownloadTests : IDisposable
{
    private static CancellationToken Ct => TestContext.Current.CancellationToken;
    private readonly string root = Path.Combine(Path.GetTempPath(), "gnazzo-download-" + Guid.NewGuid().ToString("N"));
    private readonly byte[] bytes = Enumerable.Range(0, 50000).Select(i => (byte)(i % 251)).ToArray();
    private PublicMediaAsset Asset(string revision = "v1") => new("public-demo", revision, new Uri("https://media.example.test/fixed.wav"), bytes.Length,
        Convert.ToHexString(SHA256.HashData(bytes)).ToLowerInvariant());
    private PublicMediaDownloader New(HttpMessageHandler handler, long quota = 200000, long free = 200000) =>
        new(root, quota, handler, () => free, reserveBytes: 0);
    private string Part(PublicMediaAsset asset) => Path.Combine(root, asset.StorageKey + ".part");
    private string Final(PublicMediaAsset asset) => Path.Combine(root, asset.StorageKey + ".wav");
    private static HttpResponseMessage Reply(PublicMediaAsset asset, byte[] body, int offset = 0)
    {
        var response = new HttpResponseMessage(offset == 0 ? HttpStatusCode.OK : HttpStatusCode.PartialContent)
        { Content = new ByteArrayContent(body[offset..]) };
        response.Headers.ETag = new(asset.ETag); response.Content.Headers.ContentType = new("audio/wav");
        response.Content.Headers.ContentLength = body.Length - offset;
        if (offset > 0) response.Content.Headers.ContentRange = new(offset, body.Length - 1, body.Length);
        return response;
    }
    [Fact]
    public async Task Full_download_promotes_only_verified_bytes_and_reports_progress()
    {
        var asset = Asset(); var observations = new List<DownloadProgress>();
        using var handler = new Handler(_ => Reply(asset, bytes)); using var store = New(handler);
        var path = await store.DownloadAsync(asset, observations.Add, Ct);
        Assert.Equal(bytes, await File.ReadAllBytesAsync(path, Ct)); Assert.False(File.Exists(Part(asset)));
        Assert.Equal(bytes.Length, observations[^1].Received); Assert.Equal(path, await store.GetVerifiedAsync(asset, Ct));
    }
    [Fact]
    public async Task Verified_cache_hit_performs_no_second_request()
    {
        var asset = Asset(); using var handler = new Handler(_ => Reply(asset, bytes)); using var store = New(handler);
        await store.DownloadAsync(asset, ct: Ct); await store.DownloadAsync(asset, ct: Ct); Assert.Equal(1, handler.Calls);
    }
    [Fact]
    public async Task Interrupted_transfer_resumes_after_new_downloader_instance_with_exact_range_and_validator()
    {
        var asset = Asset();
        using (var store = New(new Handler(_ =>
        {
            var response = Reply(asset, bytes); response.Content = new StreamContent(new BrokenStream(bytes, 9000));
            response.Content.Headers.ContentType = new("audio/wav"); response.Content.Headers.ContentLength = bytes.Length; return response;
        }))) await Assert.ThrowsAsync<IOException>(() => store.DownloadAsync(asset, ct: Ct));
        var count = new FileInfo(Part(asset)).Length; Assert.Equal(9000, count); Assert.False(File.Exists(Final(asset)));
        using var resumed = New(new Handler(request =>
        {
            Assert.Equal($"bytes={count}-", request.Headers.Range!.ToString()); Assert.Equal(asset.ETag, request.Headers.IfRange!.ToString());
            return Reply(asset, bytes, (int)count);
        }));
        var path = await resumed.DownloadAsync(asset, ct: Ct); Assert.Equal(bytes, await File.ReadAllBytesAsync(path, Ct));
    }
    [Fact]
    public async Task Cancellation_retains_a_partial_without_exposing_complete_file()
    {
        var asset = Asset(); using var cancel = CancellationTokenSource.CreateLinkedTokenSource(Ct);
        using var store = New(new Handler(_ => Reply(asset, bytes)));
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => store.DownloadAsync(asset, p => { if (p.Received > 0) cancel.Cancel(); }, cancel.Token));
        Assert.True(File.Exists(Part(asset))); Assert.Null(await store.GetVerifiedAsync(asset, Ct));
    }
    [Fact]
    public async Task Full_200_response_to_resume_truncates_instead_of_appending()
    {
        var asset = Asset(); Directory.CreateDirectory(root); await File.WriteAllBytesAsync(Part(asset), bytes[..1000], Ct);
        using var store = New(new Handler(_ => Reply(asset, bytes)));
        var path = await store.DownloadAsync(asset, ct: Ct); Assert.Equal(bytes, await File.ReadAllBytesAsync(path, Ct));
    }
    [Theory]
    [InlineData("start")]
    [InlineData("end")]
    [InlineData("total")]
    [InlineData("etag")]
    [InlineData("weak")]
    [InlineData("encoding")]
    [InlineData("type")]
    [InlineData("length")]
    public async Task Invalid_partial_headers_never_promote_and_remove_partial(string fault)
    {
        var asset = Asset(); Directory.CreateDirectory(root); await File.WriteAllBytesAsync(Part(asset), bytes[..1000], Ct);
        using var store = New(new Handler(_ =>
        {
            var r = Reply(asset, bytes, 1000);
            switch (fault)
            {
                case "start": r.Content.Headers.ContentRange = new(999, bytes.Length - 1, bytes.Length); break;
                case "end": r.Content.Headers.ContentRange = new(1000, bytes.Length - 2, bytes.Length); break;
                case "total": r.Content.Headers.ContentRange = new(1000, bytes.Length - 1, bytes.Length + 1); break;
                case "etag": r.Headers.ETag = new("\"different\""); break;
                case "weak": r.Headers.ETag = new(asset.ETag, isWeak: true); break;
                case "encoding": r.Content.Headers.ContentEncoding.Add("gzip"); break;
                case "type": r.Content.Headers.ContentType = new("text/html"); break;
                case "length": r.Content.Headers.ContentLength = 7; break;
            }
            return r;
        }));
        await Assert.ThrowsAsync<InvalidDataException>(() => store.DownloadAsync(asset, ct: Ct));
        Assert.False(File.Exists(Part(asset))); Assert.False(File.Exists(Final(asset)));
    }
    [Fact]
    public async Task Range_416_discards_partial_and_requires_explicit_retry()
    {
        var asset = Asset(); Directory.CreateDirectory(root); await File.WriteAllBytesAsync(Part(asset), bytes[..1000], Ct);
        using var handler = new Handler(_ => new(HttpStatusCode.RequestedRangeNotSatisfiable)); using var store = New(handler);
        await Assert.ThrowsAsync<InvalidDataException>(() => store.DownloadAsync(asset, ct: Ct));
        Assert.Equal(1, handler.Calls); Assert.False(File.Exists(Part(asset)));
    }
    [Fact]
    public async Task Hash_mismatch_does_not_create_available_media()
    {
        var asset = Asset(); var wrong = (byte[])bytes.Clone(); wrong[100] ^= 1;
        using var store = New(new Handler(_ => Reply(asset, wrong)));
        await Assert.ThrowsAsync<InvalidDataException>(() => store.DownloadAsync(asset, ct: Ct));
        Assert.Null(await store.GetVerifiedAsync(asset, Ct)); Assert.False(File.Exists(Part(asset)));
    }
    [Fact]
    public async Task Short_body_keeps_partial_for_later_range_request()
    {
        var asset = Asset(); using var store = New(new Handler(_ =>
        {
            var r = Reply(asset, bytes[..500]); r.Content.Headers.ContentLength = bytes.Length; return r;
        }));
        await Assert.ThrowsAsync<IOException>(() => store.DownloadAsync(asset, ct: Ct));
        Assert.Equal(500, new FileInfo(Part(asset)).Length); Assert.Null(await store.GetVerifiedAsync(asset, Ct));
    }
    [Fact]
    public async Task Oversized_body_cannot_exceed_trusted_file_length()
    {
        var asset = Asset(); using var store = New(new Handler(_ =>
        {
            var r = Reply(asset, bytes.Concat(new byte[1000]).ToArray()); r.Content.Headers.ContentLength = bytes.Length; return r;
        }));
        await Assert.ThrowsAsync<InvalidDataException>(() => store.DownloadAsync(asset, ct: Ct)); Assert.False(File.Exists(Final(asset)));
    }
    [Fact]
    public async Task Complete_partial_after_process_interruption_promotes_without_network()
    {
        var asset = Asset(); Directory.CreateDirectory(root); await File.WriteAllBytesAsync(Part(asset), bytes, Ct);
        using var handler = new Handler(_ => throw new InvalidOperationException()); using var store = New(handler);
        Assert.Equal(Final(asset), await store.DownloadAsync(asset, ct: Ct)); Assert.Equal(0, handler.Calls);
    }
    [Fact]
    public async Task Corrupt_complete_file_is_not_available_and_can_be_replaced()
    {
        var asset = Asset(); Directory.CreateDirectory(root); await File.WriteAllBytesAsync(Final(asset), new byte[bytes.Length], Ct);
        using var store = New(new Handler(_ => Reply(asset, bytes)));
        Assert.Null(await store.GetVerifiedAsync(asset, Ct)); await store.DownloadAsync(asset, ct: Ct);
        Assert.NotNull(await store.GetVerifiedAsync(asset, Ct));
    }
    [Fact]
    public async Task Quota_rejection_preserves_other_downloads_and_sends_no_request()
    {
        var asset = Asset(); Directory.CreateDirectory(root); var other = Path.Combine(root, "retained.part");
        await File.WriteAllBytesAsync(other, new byte[100], Ct);
        using var handler = new Handler(_ => Reply(asset, bytes)); using var store = New(handler, quota: bytes.Length);
        await Assert.ThrowsAsync<IOException>(() => store.DownloadAsync(asset, ct: Ct));
        Assert.Equal(0, handler.Calls); Assert.True(File.Exists(other));
    }
    [Fact]
    public async Task Low_free_space_fails_before_request_or_partial_creation()
    {
        var asset = Asset(); using var handler = new Handler(_ => Reply(asset, bytes)); using var store = New(handler, free: bytes.Length - 1);
        await Assert.ThrowsAsync<IOException>(() => store.DownloadAsync(asset, ct: Ct)); Assert.Equal(0, handler.Calls); Assert.False(File.Exists(Part(asset)));
    }
    [Fact]
    public async Task Deletion_removes_only_selected_complete_and_partial_files()
    {
        var asset = Asset(); var other = Asset("v2"); Directory.CreateDirectory(root);
        await File.WriteAllBytesAsync(Part(asset), bytes[..100], Ct); await File.WriteAllBytesAsync(Final(asset), bytes, Ct);
        await File.WriteAllBytesAsync(Part(other), bytes[..100], Ct); using var store = New(new Handler(_ => Reply(asset, bytes)));
        await store.DeleteAsync(asset, Ct); Assert.False(File.Exists(Part(asset))); Assert.False(File.Exists(Final(asset))); Assert.True(File.Exists(Part(other)));
    }
    [Theory]
    [InlineData("../escape")]
    [InlineData("C:\\media")]
    [InlineData("nested/name")]
    public void Untrusted_names_cannot_become_paths(string id) => Assert.Throws<ArgumentException>(() => new PublicMediaAsset(id, "v1", Asset().Source, bytes.Length, Asset().Sha256));
    [Theory]
    [InlineData("http://media.example.test/a")]
    [InlineData("https://user:pass@media.example.test/a")]
    [InlineData("https://media.example.test/a?token=secret")]
    [InlineData("https://media.example.test/a#fragment")]
    public void Insecure_or_credential_bearing_sources_are_rejected(string uri) => Assert.Throws<ArgumentException>(() => new PublicMediaAsset("a", "v1", new(uri), bytes.Length, Asset().Sha256));
    [Fact]
    public void Private_delivery_cannot_enter_public_anonymous_storage() => Assert.Throws<NotSupportedException>(() => new PublicMediaAsset("a", "v1", Asset().Source, bytes.Length, Asset().Sha256, MediaAccess.PrivateDelivery));
    [Fact]
    public void Revision_and_origin_are_part_of_safe_storage_identity()
    {
        var a = Asset(); var b = Asset("v2"); var c = new PublicMediaAsset(a.Id, a.Revision, new("https://other.example.test/fixed.wav"), a.Length, a.Sha256);
        Assert.NotEqual(a.StorageKey, b.StorageKey); Assert.NotEqual(a.StorageKey, c.StorageKey); Assert.Matches("^[a-f0-9]{64}$", a.StorageKey);
    }
    [Theory]
    [InlineData(HttpStatusCode.Redirect)]
    [InlineData(HttpStatusCode.NotFound)]
    public async Task Redirect_and_error_status_are_not_media(HttpStatusCode status)
    {
        using var handler = new Handler(_ => new(status)); using var store = New(handler);
        await Assert.ThrowsAsync<HttpRequestException>(() => store.DownloadAsync(Asset(), ct: Ct)); Assert.Equal(1, handler.Calls);
    }
    [Fact]
    public async Task Deletion_waits_for_active_transfer_before_removing_its_files()
    {
        var asset = Asset(); using var store = New(new Handler(_ => Reply(asset, bytes))); Task? deletion = null;
        await store.DownloadAsync(asset, p =>
        {
            if (p.Received <= 0 || deletion is not null) return;
            deletion = store.DeleteAsync(asset, Ct); Assert.False(deletion.IsCompleted);
        }, Ct);
        Assert.NotNull(deletion); await deletion; Assert.Null(await store.GetVerifiedAsync(asset, Ct));
    }
    [Fact]
    public async Task Free_space_reserve_is_included_before_network_work()
    {
        using var handler = new Handler(_ => Reply(Asset(), bytes));
        using var store = new PublicMediaDownloader(root, handler: handler, availableBytes: () => bytes.Length, reserveBytes: 1);
        await Assert.ThrowsAsync<IOException>(() => store.DownloadAsync(Asset(), ct: Ct)); Assert.Equal(0, handler.Calls);
    }
    private sealed class Handler(Func<HttpRequestMessage, HttpResponseMessage> reply) : HttpMessageHandler
    {
        public int Calls;
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken ct)
        { Calls++; Assert.Null(request.Headers.Authorization); return Task.FromResult(reply(request)); }
    }
    private sealed class BrokenStream(byte[] body, int limit) : MemoryStream(body)
    {
        public override ValueTask<int> ReadAsync(Memory<byte> buffer, CancellationToken ct = default)
        {
            if (Position >= limit) throw new IOException("Simulated transport interruption.");
            return base.ReadAsync(buffer[..Math.Min(buffer.Length, limit - (int)Position)], ct);
        }
    }
    public void Dispose() { if (Directory.Exists(root)) Directory.Delete(root, recursive: true); }
}

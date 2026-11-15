using System.Net;
using System.Net.Http.Headers;
using System.Security.Cryptography;
using GnazzosWorld.Core.Downloads;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Xunit;

namespace GnazzosWorld.Api.Tests;

public sealed class MediaDownloadApiTests
{
    private static CancellationToken Ct => TestContext.Current.CancellationToken;
    private sealed class Factory : WebApplicationFactory<Program>
    {
        protected override void ConfigureWebHost(IWebHostBuilder builder)
        {
            builder.UseEnvironment("Development");
            builder.UseContentRoot(Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "../../../../../src/GnazzosWorld.Api")));
        }
        public HttpClient Client() => CreateClient(new() { BaseAddress = new("https://localhost"), AllowAutoRedirect = false });
    }
    [Fact]
    public async Task Public_fixture_body_matches_trusted_descriptor_and_strong_validator()
    {
        using var factory = new Factory(); using var client = factory.Client();
        using var r = await client.GetAsync("/api/v1/media/timing", Ct); var body = await r.Content.ReadAsByteArrayAsync(Ct);
        Assert.Equal(HttpStatusCode.OK, r.StatusCode); Assert.Equal(160044, body.Length);
        Assert.Equal("audio/wav", r.Content.Headers.ContentType!.MediaType);
        Assert.Equal(PublicDownloadFixture.Hash, Convert.ToHexString(SHA256.HashData(body)).ToLowerInvariant());
        Assert.Equal(PublicDownloadFixture.At(client.BaseAddress!).ETag, r.Headers.ETag!.ToString()); Assert.False(r.Headers.ETag.IsWeak);
    }
    [Fact]
    public async Task Matching_IfRange_returns_exact_requested_slice()
    {
        using var factory = new Factory(); using var client = factory.Client();
        var original = await client.GetByteArrayAsync("/api/v1/media/timing", Ct);
        using var request = new HttpRequestMessage(HttpMethod.Get, "/api/v1/media/timing");
        request.Headers.Range = new(100, 199); request.Headers.IfRange = new(new EntityTagHeaderValue(PublicDownloadFixture.At(client.BaseAddress!).ETag));
        using var r = await client.SendAsync(request, Ct);
        Assert.Equal(HttpStatusCode.PartialContent, r.StatusCode); Assert.Equal("bytes 100-199/160044", r.Content.Headers.ContentRange!.ToString());
        Assert.Equal(original[100..200], await r.Content.ReadAsByteArrayAsync(Ct));
    }
    [Fact]
    public async Task Changed_IfRange_returns_full_200_instead_of_an_appendable_slice()
    {
        using var factory = new Factory(); using var client = factory.Client();
        using var request = new HttpRequestMessage(HttpMethod.Get, "/api/v1/media/timing"); request.Headers.Range = new(100, null);
        request.Headers.IfRange = new(new EntityTagHeaderValue("\"old-revision\"")); using var r = await client.SendAsync(request, Ct);
        Assert.Equal(HttpStatusCode.OK, r.StatusCode); Assert.Null(r.Content.Headers.ContentRange); Assert.Equal(160044, (await r.Content.ReadAsByteArrayAsync(Ct)).Length);
    }
    [Fact]
    public async Task Unsatisfiable_range_is_416_with_current_length()
    {
        using var factory = new Factory(); using var client = factory.Client();
        using var request = new HttpRequestMessage(HttpMethod.Get, "/api/v1/media/timing"); request.Headers.Range = new(999999, null);
        using var r = await client.SendAsync(request, Ct); Assert.Equal(HttpStatusCode.RequestedRangeNotSatisfiable, r.StatusCode);
        Assert.Equal("bytes */160044", r.Content.Headers.ContentRange!.ToString());
    }
    [Fact]
    public async Task Head_exposes_length_without_a_media_body()
    {
        using var factory = new Factory(); using var client = factory.Client();
        using var request = new HttpRequestMessage(HttpMethod.Head, "/api/v1/media/timing"); using var r = await client.SendAsync(request, Ct);
        Assert.Equal(HttpStatusCode.OK, r.StatusCode); Assert.Equal(160044, r.Content.Headers.ContentLength);
        Assert.Empty(await r.Content.ReadAsByteArrayAsync(Ct));
    }
    [Fact]
    public async Task No_private_media_path_is_supplied_by_public_fixture_route()
    {
        using var factory = new Factory(); using var client = factory.Client();
        using var r = await client.GetAsync("/api/v1/media/private/customer-delivery", Ct); Assert.Equal(HttpStatusCode.NotFound, r.StatusCode);
    }
    [Fact]
    public async Task Actual_downloader_resumes_an_interrupted_transfer_against_range_capable_server_pipeline()
    {
        using var factory = new Factory(); using var bootstrap = factory.Client();
        var root = Path.Combine(Path.GetTempPath(), "gnazzo-api-media-" + Guid.NewGuid().ToString("N"));
        var asset = PublicDownloadFixture.At(new("https://localhost"));
        try
        {
            using (var canceled = CancellationTokenSource.CreateLinkedTokenSource(Ct))
            using (var first = new PublicMediaDownloader(root, handler: factory.Server.CreateHandler(), availableBytes: () => 2000000))
            {
                await Assert.ThrowsAnyAsync<OperationCanceledException>(() => first.DownloadAsync(asset, p => { if (p.Received > 0) canceled.Cancel(); }, canceled.Token));
            }
            var partial = new FileInfo(Path.Combine(root, asset.StorageKey + ".part")).Length;
            Assert.InRange(partial, 1, asset.Length - 1);
            using var handler = new TraceHandler(factory.Server.CreateHandler());
            using var second = new PublicMediaDownloader(root, handler: handler, availableBytes: () => 2000000);
            var path = await second.DownloadAsync(asset, ct: Ct);
            Assert.Equal($"bytes={partial}-", handler.Range); Assert.Equal(asset.ETag, handler.IfRange);
            Assert.Equal(HttpStatusCode.PartialContent, handler.Status); Assert.Equal(path, await second.GetVerifiedAsync(asset, Ct));
        }
        finally { if (Directory.Exists(root)) Directory.Delete(root, recursive: true); }
    }
    private sealed class TraceHandler(HttpMessageHandler inner) : DelegatingHandler(inner)
    {
        public string? Range, IfRange; public HttpStatusCode Status;
        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken ct)
        {
            Range = request.Headers.Range?.ToString(); IfRange = request.Headers.IfRange?.ToString();
            var response = await base.SendAsync(request, ct); Status = response.StatusCode; return response;
        }
    }
}

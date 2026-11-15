using System.Net;
using System.Net.Http.Headers;
using System.Security.Cryptography;
using System.Text;

namespace GnazzosWorld.Core.Downloads;

public enum MediaAccess { PublicSample, PrivateDelivery }
public sealed record PublicMediaAsset
{
    public string Id { get; }
    public string Revision { get; }
    public Uri Source { get; }
    public long Length { get; }
    public string Sha256 { get; }
    public string ETag => "\"" + Sha256 + "\"";
    public string StorageKey { get; }
    public PublicMediaAsset(string id, string revision, Uri source, long length, string sha256,
        MediaAccess access = MediaAccess.PublicSample)
    {
        if (access != MediaAccess.PublicSample) throw new NotSupportedException("Private deliveries require a separate authenticated, owner-scoped design.");
        static bool Key(string value) => value.Length is > 0 and <= 64 && value.All(c => char.IsAsciiLetterOrDigit(c) || c is '-' or '_');
        if (!Key(id) || !Key(revision)) throw new ArgumentException("Use bounded media identities.");
        if (!source.IsAbsoluteUri || source.Scheme != Uri.UriSchemeHttps || !string.IsNullOrEmpty(source.UserInfo) ||
            !string.IsNullOrEmpty(source.Query) || !string.IsNullOrEmpty(source.Fragment))
            throw new ArgumentException("Use a fixed HTTPS public media URI without credentials, query or fragment.");
        if (length is <= 0 or > 8 * 1024 * 1024 || sha256.Length != 64 || !sha256.All(c => c is >= '0' and <= '9' or >= 'a' and <= 'f'))
            throw new ArgumentException("Use a bounded length and lowercase SHA-256 from a trusted descriptor.");
        Id = id; Revision = revision; Source = source; Length = length; Sha256 = sha256;
        StorageKey = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes($"{id}\n{revision}\n{source.AbsoluteUri}\n{length}\n{sha256}"))).ToLowerInvariant();
    }
}
public sealed record DownloadProgress(long Received, long Total, bool Resuming);

/// <summary>One instance per dedicated public-cache folder; serializes transfers and deletion.
/// Handler injection is a trusted composition/test boundary. Production redirects/decompression are disabled.</summary>
public sealed class PublicMediaDownloader : IDisposable
{
    private readonly string root;
    private readonly HttpClient http;
    private readonly SemaphoreSlim gate = new(1, 1);
    private readonly long quota;
    private readonly long reserve;
    private readonly Func<long> freeBytes;
    private readonly TimeSpan timeout;
    public PublicMediaDownloader(string root, long quotaBytes = 4 * 1024 * 1024,
        HttpMessageHandler? handler = null, Func<long>? availableBytes = null,
        long reserveBytes = 64 * 1024, TimeSpan? transferTimeout = null)
    {
        if (quotaBytes <= 0 || reserveBytes < 0) throw new ArgumentOutOfRangeException(nameof(quotaBytes));
        this.root = Path.GetFullPath(root); Directory.CreateDirectory(this.root);
        quota = quotaBytes; reserve = reserveBytes;
        freeBytes = availableBytes ?? (() => new DriveInfo(Path.GetPathRoot(this.root)!).AvailableFreeSpace);
        timeout = transferTimeout ?? TimeSpan.FromSeconds(30);
        if (timeout <= TimeSpan.Zero) throw new ArgumentOutOfRangeException(nameof(transferTimeout));
        http = new(handler ?? new HttpClientHandler { AllowAutoRedirect = false, AutomaticDecompression = DecompressionMethods.None });
        http.Timeout = Timeout.InfiniteTimeSpan;
    }
    private string Part(PublicMediaAsset asset) => Path.Combine(root, asset.StorageKey + ".part");
    private string Final(PublicMediaAsset asset) => Path.Combine(root, asset.StorageKey + ".wav");
    public async Task<string> DownloadAsync(PublicMediaAsset asset, Action<DownloadProgress>? progress = null, CancellationToken ct = default)
    {
        await gate.WaitAsync(ct);
        using var deadline = CancellationTokenSource.CreateLinkedTokenSource(ct);
        deadline.CancelAfter(timeout); var token = deadline.Token;
        try
        {
            if (await ValidAsync(Final(asset), asset, token)) return Final(asset);
            File.Delete(Final(asset));
            var part = Part(asset); var offset = File.Exists(part) ? new FileInfo(part).Length : 0;
            if (offset > asset.Length) { File.Delete(part); offset = 0; }
            if (offset == asset.Length)
            {
                if (await ValidAsync(part, asset, token)) return Promote(asset);
                File.Delete(part); offset = 0;
            }
            EnsureBudget(asset, offset);
            using var request = new HttpRequestMessage(HttpMethod.Get, asset.Source);
            request.Headers.AcceptEncoding.ParseAdd("identity");
            if (offset > 0)
            {
                request.Headers.Range = new RangeHeaderValue(offset, null);
                request.Headers.IfRange = new RangeConditionHeaderValue(new EntityTagHeaderValue(asset.ETag));
            }
            using var response = await http.SendAsync(request, HttpCompletionOption.ResponseHeadersRead, token);
            if (response.StatusCode == HttpStatusCode.RequestedRangeNotSatisfiable)
                throw new InvalidDataException("Range rejected. Partial removed; retry explicitly from zero.");
            if (response.StatusCode is not HttpStatusCode.OK and not HttpStatusCode.PartialContent)
                throw new HttpRequestException("Public media endpoint did not return a supported representation.", null, response.StatusCode);
            if (response.Headers.ETag?.ToString() != asset.ETag || response.Content.Headers.ContentEncoding.Any(x => x != "identity") ||
                response.Content.Headers.ContentType?.MediaType != "audio/wav")
                throw new InvalidDataException("Public media validator, encoding or type did not match.");
            var range = response.Content.Headers.ContentRange;
            if (response.StatusCode == HttpStatusCode.PartialContent)
            {
                if (offset == 0 || range is null || range.Unit != "bytes" || range.From != offset ||
                    range.To != asset.Length - 1 || range.Length != asset.Length)
                    throw new InvalidDataException("Unexpected partial representation.");
            }
            else
            {
                if (range is not null) throw new InvalidDataException("Unexpected Content-Range on full response.");
                offset = 0; // A server may ignore Range/If-Range. Truncate; never append its full body.
            }
            var expected = asset.Length - offset;
            if (response.Content.Headers.ContentLength != expected) throw new InvalidDataException("Unexpected representation length.");
            await using (var input = await response.Content.ReadAsStreamAsync(token))
            await using (var output = new FileStream(part, offset == 0 ? FileMode.Create : FileMode.Append,
                FileAccess.Write, FileShare.None, 8192, FileOptions.Asynchronous))
            {
                var received = offset; var buffer = new byte[8192]; progress?.Invoke(new(received, asset.Length, offset > 0));
                while (true)
                {
                    var read = await input.ReadAsync(buffer, token);
                    if (read == 0) break;
                    if (received + read > asset.Length) throw new InvalidDataException("Media body exceeded the trusted length.");
                    await output.WriteAsync(buffer.AsMemory(0, read), token); received += read;
                    progress?.Invoke(new(received, asset.Length, offset > 0));
                }
                if (received != asset.Length) throw new IOException("Transfer ended early; partial retained for explicit resume.");
                await output.FlushAsync(token);
            }
            token.ThrowIfCancellationRequested();
            if (!await ValidAsync(part, asset, token)) throw new InvalidDataException("Complete media hash did not match.");
            token.ThrowIfCancellationRequested(); return Promote(asset);
        }
        catch (InvalidDataException) { File.Delete(Part(asset)); throw; }
        finally { gate.Release(); }
    }
    private void EnsureBudget(PublicMediaAsset asset, long offset)
    {
        var used = Directory.EnumerateFiles(root).Sum(path => new FileInfo(path).Length);
        if (asset.Length > quota || used - offset > quota - asset.Length)
            throw new IOException("Public media quota exceeded. Delete a download or partial explicitly.");
        var needed = checked(asset.Length - offset + reserve);
        if (freeBytes() < needed) throw new IOException("Insufficient free space for remaining bytes and reserve.");
    }
    private string Promote(PublicMediaAsset asset)
    {
        File.Move(Part(asset), Final(asset), overwrite: true); return Final(asset);
    }
    private static async Task<bool> ValidAsync(string path, PublicMediaAsset asset, CancellationToken ct)
    {
        if (!File.Exists(path) || new FileInfo(path).Length != asset.Length) return false;
        await using var file = File.OpenRead(path);
        return Convert.ToHexString(await SHA256.HashDataAsync(file, ct)).ToLowerInvariant() == asset.Sha256;
    }
    public async Task<string?> GetVerifiedAsync(PublicMediaAsset asset, CancellationToken ct = default)
    {
        await gate.WaitAsync(ct);
        try { return await ValidAsync(Final(asset), asset, ct) ? Final(asset) : null; }
        finally { gate.Release(); }
    }
    public async Task DeleteAsync(PublicMediaAsset asset, CancellationToken ct = default)
    {
        await gate.WaitAsync(ct);
        try { File.Delete(Final(asset)); File.Delete(Part(asset)); }
        finally { gate.Release(); }
    }
    // Composition must stop/await active operations before disposal; no concurrent Dispose contract.
    public void Dispose() { http.Dispose(); gate.Dispose(); }
}

public static class PublicDownloadFixture
{
    public const string Hash = "d72025ca39105344dd24b0353eeb3f019c8c6fabfed0390a85988df48a7e284f";
    public static PublicMediaAsset At(Uri origin) => new("timing-chimes", "pcm-v1", new Uri(origin, "/api/v1/media/timing"), 160044, Hash);
}

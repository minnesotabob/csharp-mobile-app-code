using System.Buffers.Binary;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using GnazzosWorld.Core.Attachments;
using Xunit;

namespace GnazzosWorld.Core.Tests;

public sealed class AttachmentTests : IDisposable
{
    private static CancellationToken Ct => TestContext.Current.CancellationToken;
    private readonly string root = Path.Combine(Path.GetTempPath(), "gnazzo-attachment-" + Guid.NewGuid().ToString("N"));
    private string WorkRoot => Path.Combine(root, "work");
    private string ShareRoot => Path.Combine(root, "sharing-root");
    private static byte[] Fixture(string name = "clean")
    {
        var repo = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "../../../../../"));
        using var doc = JsonDocument.Parse(File.ReadAllText(Path.Combine(repo, "samples/attachment-fixtures.json")));
        var row = Assert.Single(doc.RootElement.GetProperty("files").EnumerateArray(), x => x.GetProperty("name").GetString() == name);
        var bytes = Convert.FromBase64String(row.GetProperty("base64").GetString()!);
        Assert.Equal(row.GetProperty("sha256").GetString(), Convert.ToHexString(SHA256.HashData(bytes)).ToLowerInvariant()); return bytes;
    }
    private static Task<Stream> Open(byte[] data) => Task.FromResult<Stream>(new MemoryStream(data, writable: false));
    private AttachmentWorkspace Workspace(IAttachmentImageNormalizer? normalizer = null) => new(WorkRoot, normalizer ?? new Normalizer(Fixture()));
    private string Staged(AttachmentReceipt r) => Assert.Single(Directory.EnumerateFiles(WorkRoot, r.Id + r.Extension, SearchOption.AllDirectories));
    private static AttachmentReceipt Receipt(byte[] data, AttachmentKind kind) => new(Guid.NewGuid().ToString("N"), kind, data.Length, Convert.ToHexString(SHA256.HashData(data)).ToLowerInvariant());
    [Fact]
    public async Task Text_is_staged_under_generated_name_and_reverified()
    {
        var w = Workspace(); var bytes = Encoding.UTF8.GetBytes("Synthetic anniversary reference\n");
        var r = await w.StageAsync(AttachmentKind.Text, _ => Open(bytes), w.Generation, Ct);
        Assert.Matches("^[a-f0-9]{32}$", r.Id); Assert.Equal(".txt", r.Extension); Assert.Equal(bytes, await w.ReadCurrentAsync(w.Generation, Ct));
        Assert.True(File.Exists(Staged(r))); Assert.DoesNotContain("Synthetic", Path.GetFileName(Staged(r)));
    }
    [Fact]
    public async Task Nonseekable_provider_stream_is_supported_and_disposed()
    {
        var w = Workspace(); var stream = new ProviderStream("reference"u8.ToArray());
        await w.StageAsync(AttachmentKind.Text, _ => Task.FromResult<Stream>(stream), w.Generation, Ct); Assert.True(stream.Disposed);
    }
    [Theory]
    [InlineData(0)]
    [InlineData(65537)]
    public async Task Empty_or_oversized_text_is_rejected(int length)
    { var w = Workspace(); await Assert.ThrowsAsync<InvalidDataException>(() => w.StageAsync(AttachmentKind.Text, _ => Open(new byte[length]), w.Generation, Ct)); Assert.Null(w.Current); }
    [Fact]
    public async Task Invalid_utf8_is_rejected_without_replacement()
    { var w = Workspace(); await Assert.ThrowsAsync<DecoderFallbackException>(() => w.StageAsync(AttachmentKind.Text, _ => Open([0xc3, 0x28]), w.Generation, Ct)); Assert.Null(w.Current); }
    [Fact]
    public async Task Embedded_nul_is_not_accepted_as_plain_text()
    { var w = Workspace(); await Assert.ThrowsAsync<InvalidDataException>(() => w.StageAsync(AttachmentKind.Text, _ => Open([65, 0, 66]), w.Generation, Ct)); }
    [Fact]
    public async Task Failed_replacement_preserves_previous_confirmed_attachment()
    {
        var normalizer = new Normalizer(Fixture()) { Fail = true }; var w = Workspace(normalizer);
        var first = await w.StageAsync(AttachmentKind.Text, _ => Open("first"u8.ToArray()), w.Generation, Ct);
        await Assert.ThrowsAsync<IOException>(() => w.StageAsync(AttachmentKind.Photo, _ => Open(Fixture()), w.Generation, Ct));
        Assert.Equal(first, w.Current); Assert.Equal("first"u8.ToArray(), await w.ReadCurrentAsync(w.Generation, Ct));
    }
    [Fact]
    public async Task Successful_replacement_removes_previous_file()
    {
        var w = Workspace(); var first = await w.StageAsync(AttachmentKind.Text, _ => Open("first"u8.ToArray()), w.Generation, Ct); var old = Staged(first);
        var next = await w.StageAsync(AttachmentKind.Text, _ => Open("next"u8.ToArray()), w.Generation, Ct);
        Assert.False(File.Exists(old)); Assert.True(File.Exists(Staged(next))); Assert.Equal(next, w.Current);
    }
    [Fact]
    public async Task Native_normalizer_output_has_ancillary_metadata_removed_before_staging()
    {
        var n = new Normalizer(Fixture("synthetic-metadata")); var w = Workspace(n);
        var r = await w.StageAsync(AttachmentKind.Photo, _ => Open(Fixture()), w.Generation, Ct);
        Assert.Equal(1, n.Calls); Assert.Equal("image/png", r.MediaType); Assert.Equal(Fixture(), await w.ReadCurrentAsync(w.Generation, Ct));
    }
    [Fact]
    public async Task Input_byte_limit_is_checked_before_normalizer()
    {
        var n = new Normalizer(Fixture()); var w = Workspace(n);
        await Assert.ThrowsAsync<InvalidDataException>(() => w.StageAsync(AttachmentKind.Photo, _ => Open(new byte[AttachmentWorkspace.MaximumInputImageBytes + 1]), w.Generation, Ct)); Assert.Equal(0, n.Calls);
    }
    [Fact]
    public async Task Input_pixel_budget_is_checked_before_normalizer()
    {
        var n = new Normalizer(Fixture()); var w = Workspace(n); var oversized = Fixture();
        BinaryPrimitives.WriteUInt32BigEndian(oversized.AsSpan(16, 4), 3000); BinaryPrimitives.WriteUInt32BigEndian(oversized.AsSpan(20, 4), 3000);
        await Assert.ThrowsAsync<InvalidDataException>(() => w.StageAsync(AttachmentKind.Photo, _ => Open(oversized), w.Generation, Ct)); Assert.Equal(0, n.Calls);
    }
    [Fact]
    public async Task Claimed_extension_cannot_make_nonimage_bytes_decodable()
    {
        var n = new Normalizer(Fixture()); var w = Workspace(n);
        await Assert.ThrowsAsync<InvalidDataException>(() => w.StageAsync(AttachmentKind.Photo, _ => Open("not a png or jpeg"u8.ToArray()), w.Generation, Ct)); Assert.Equal(0, n.Calls);
    }
    [Fact]
    public async Task Native_encoder_output_is_bounded_independently_of_dimensions()
    {
        var w = Workspace(new Normalizer(new byte[AttachmentWorkspace.MaximumOutputBytes + 1]));
        await Assert.ThrowsAsync<InvalidDataException>(() => w.StageAsync(AttachmentKind.Photo, _ => Open(Fixture()), w.Generation, Ct)); Assert.Null(w.Current);
    }
    [Fact]
    public async Task Corrupt_normalized_png_is_rejected()
    {
        var broken = Fixture(); broken[^1] ^= 1; var w = Workspace(new Normalizer(broken));
        await Assert.ThrowsAsync<InvalidDataException>(() => w.StageAsync(AttachmentKind.Photo, _ => Open(Fixture()), w.Generation, Ct)); Assert.Null(w.Current);
    }
    [Fact]
    public void Png_stripping_retains_exact_pixel_chunks_from_independently_generated_fixture()
    { Assert.Equal(Fixture(), ImageEnvelope.PixelOnlyPng(Fixture("synthetic-metadata"))); Assert.Equal(new ImageShape("png", 4, 3), ImageEnvelope.Inspect(Fixture())); }
    [Fact]
    public void Trailing_bytes_after_png_ending_are_rejected() => Assert.Throws<InvalidDataException>(() => ImageEnvelope.PixelOnlyPng(Fixture().Concat(new byte[] { 1 }).ToArray()));
    [Fact]
    public void Truncated_png_chunk_is_rejected() => Assert.Throws<InvalidDataException>(() => ImageEnvelope.PixelOnlyPng(Fixture()[..^3]));
    [Theory]
    [InlineData(0, 3)]
    [InlineData(4, 0)]
    [InlineData(5000, 1)]
    public void Invalid_image_dimensions_fail_preflight(int width, int height)
    {
        var input = Fixture(); BinaryPrimitives.WriteUInt32BigEndian(input.AsSpan(16, 4), (uint)width); BinaryPrimitives.WriteUInt32BigEndian(input.AsSpan(20, 4), (uint)height);
        Assert.Throws<InvalidDataException>(() => ImageEnvelope.Inspect(input));
    }
    [Fact]
    public void Synthetic_jpeg_frame_header_can_be_preflighted_without_claiming_decode()
    {
        byte[] header = [255, 216, 255, 192, 0, 8, 8, 0, 3, 0, 4, 1, 255, 217];
        Assert.Equal(new ImageShape("jpeg", 4, 3), ImageEnvelope.Inspect(header));
    }
    [Fact]
    public async Task Clear_invalidates_a_late_cancellation_ignoring_provider_open_and_disposes_stream()
    {
        var w = Workspace(); var opened = new TaskCompletionSource<Stream>(TaskCreationOptions.RunContinuationsAsynchronously);
        var stream = new ProviderStream("late"u8.ToArray()); var attempt = w.StageAsync(AttachmentKind.Text, _ => opened.Task, w.Generation, Ct);
        var clearing = w.ClearAsync(Ct); opened.SetResult(stream);
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => attempt); await clearing;
        Assert.Null(w.Current); Assert.True(stream.Disposed); Assert.False(w.CleanupUnconfirmed);
    }
    [Fact]
    public async Task Stale_scope_is_rejected_before_provider_is_opened()
    {
        var w = Workspace(); var old = w.Generation; await w.ClearAsync(Ct); var calls = 0;
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => w.StageAsync(AttachmentKind.Text, _ => { calls++; return Open("late"u8.ToArray()); }, old, Ct)); Assert.Equal(0, calls);
    }
    [Fact]
    public async Task Cancellation_does_not_replace_prior_selection()
    {
        var w = Workspace(); var first = await w.StageAsync(AttachmentKind.Text, _ => Open("first"u8.ToArray()), w.Generation, Ct);
        using var canceled = CancellationTokenSource.CreateLinkedTokenSource(Ct); canceled.Cancel();
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => w.StageAsync(AttachmentKind.Text, _ => Open("next"u8.ToArray()), w.Generation, canceled.Token)); Assert.Equal(first, w.Current);
    }
    [Fact]
    public async Task Canceled_cleanup_clears_use_immediately_but_reports_unconfirmed_disk_cleanup()
    {
        var w = Workspace(); var r = await w.StageAsync(AttachmentKind.Text, _ => Open("first"u8.ToArray()), w.Generation, Ct); var path = Staged(r);
        using var canceled = CancellationTokenSource.CreateLinkedTokenSource(Ct); canceled.Cancel();
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => w.ClearAsync(canceled.Token)); Assert.Null(w.Current); Assert.True(w.CleanupUnconfirmed); Assert.True(File.Exists(path));
        await w.ClearAsync(Ct); Assert.False(w.CleanupUnconfirmed); Assert.False(File.Exists(path));
    }
    [Fact]
    public async Task Staged_integrity_is_rechecked_before_preview_or_export()
    {
        var w = Workspace(); var r = await w.StageAsync(AttachmentKind.Text, _ => Open("first"u8.ToArray()), w.Generation, Ct);
        await File.WriteAllBytesAsync(Staged(r), "wrong"u8.ToArray(), Ct);
        await Assert.ThrowsAsync<InvalidDataException>(() => w.ReadCurrentAsync(w.Generation, Ct));
    }
    [Fact]
    public async Task Aggregate_cache_budget_counts_abandoned_scope_bytes()
    {
        var w = Workspace(); var other = Path.Combine(WorkRoot, "abandoned"); Directory.CreateDirectory(other);
        using (var large = File.Create(Path.Combine(other, "retained.bin"))) large.SetLength(16 * 1024 * 1024);
        await Assert.ThrowsAsync<IOException>(() => w.StageAsync(AttachmentKind.Text, _ => Open("next"u8.ToArray()), w.Generation, Ct)); Assert.Null(w.Current);
    }
    [Fact]
    public async Task Export_uses_generated_name_and_exact_verified_content()
    {
        var clock = new Clock(); var exports = new AttachmentExportStore(ShareRoot, clock); var data = Fixture(); var r = Receipt(data, AttachmentKind.Photo);
        var path = await exports.CreateAsync(r, data, Ct); Assert.Equal(data, await File.ReadAllBytesAsync(path, Ct));
        Assert.Equal(ShareRoot, Path.GetDirectoryName(path)); Assert.Matches("^[a-f0-9]{32}\\.png$", Path.GetFileName(path));
    }
    [Fact]
    public async Task Export_rejects_hash_mismatch()
    {
        var store = new AttachmentExportStore(ShareRoot, new Clock()); var data = "reference"u8.ToArray();
        await Assert.ThrowsAsync<InvalidDataException>(() => store.CreateAsync(Receipt(data, AttachmentKind.Text) with { Sha256 = new string('0', 64) }, data, Ct));
    }
    [Fact]
    public async Task Export_rejects_png_metadata_even_with_matching_forged_receipt()
    {
        var store = new AttachmentExportStore(ShareRoot, new Clock()); var data = Fixture("synthetic-metadata");
        await Assert.ThrowsAsync<InvalidDataException>(() => store.CreateAsync(Receipt(data, AttachmentKind.Photo), data, Ct));
    }
    [Fact]
    public async Task Export_count_cap_rejects_fifth_without_deleting_previous_copies()
    {
        var store = new AttachmentExportStore(ShareRoot, new Clock()); var data = "reference"u8.ToArray(); var r = Receipt(data, AttachmentKind.Text);
        for (var i = 0; i < 4; i++) await store.CreateAsync(r, data, Ct);
        await Assert.ThrowsAsync<IOException>(() => store.CreateAsync(r, data, Ct)); Assert.Equal(4, Directory.GetFiles(ShareRoot).Length);
    }
    [Fact]
    public async Task Expired_share_copies_are_pruned_only_when_another_export_is_requested()
    {
        var clock = new Clock(); var store = new AttachmentExportStore(ShareRoot, clock); var data = "reference"u8.ToArray(); var r = Receipt(data, AttachmentKind.Text);
        var first = await store.CreateAsync(r, data, Ct); clock.Now = clock.Now.AddHours(25); Assert.True(File.Exists(first));
        await store.CreateAsync(r, data, Ct); Assert.False(File.Exists(first)); Assert.Single(Directory.GetFiles(ShareRoot));
    }
    [Fact]
    public async Task Explicit_export_cleanup_does_not_delete_unknown_files()
    {
        var store = new AttachmentExportStore(ShareRoot, new Clock()); var data = "reference"u8.ToArray();
        var path = await store.CreateAsync(Receipt(data, AttachmentKind.Text), data, Ct); var other = Path.Combine(ShareRoot, "unknown.keep"); await File.WriteAllTextAsync(other, "synthetic", Ct);
        await store.ClearAsync(Ct); Assert.False(File.Exists(path)); Assert.True(File.Exists(other));
    }
    [Fact]
    public void Optional_haptic_is_not_invoked_when_disabled()
    { var count = 0; Assert.False(CapabilityFallback.RequestOptionalHaptic(false, () => count++)); Assert.Equal(0, count); }
    [Fact]
    public void Unsupported_haptic_does_not_turn_a_completed_attachment_into_failure()
    { Assert.False(CapabilityFallback.RequestOptionalHaptic(true, () => throw new NotSupportedException())); }
    private sealed class Clock : TimeProvider
    { public DateTimeOffset Now = new(2026, 11, 15, 12, 0, 0, TimeSpan.Zero); public override DateTimeOffset GetUtcNow() => Now; }
    private sealed class Normalizer(byte[] result) : IAttachmentImageNormalizer
    {
        public bool Fail; public int Calls;
        public Task NormalizeAsync(Stream source, Stream destination, int maxEdge, CancellationToken ct)
        { Calls++; if (Fail) throw new IOException("Synthetic native failure."); return destination.WriteAsync(result, ct).AsTask(); }
    }
    private sealed class ProviderStream(byte[] data) : MemoryStream(data)
    {
        public bool Disposed; public override bool CanSeek => false;
        public override long Length => throw new NotSupportedException();
        protected override void Dispose(bool disposing) { Disposed = true; base.Dispose(disposing); }
    }
    public void Dispose() { if (Directory.Exists(root)) Directory.Delete(root, recursive: true); }
}

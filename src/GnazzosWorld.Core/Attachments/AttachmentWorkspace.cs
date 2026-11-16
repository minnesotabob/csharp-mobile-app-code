using System.Security.Cryptography;
using System.Text;

namespace GnazzosWorld.Core.Attachments;

public enum AttachmentKind { Text, Photo }
public sealed record AttachmentReceipt(string Id, AttachmentKind Kind, int Length, string Sha256)
{
    public string Extension => Kind == AttachmentKind.Text ? ".txt" : ".png";
    public string MediaType => Kind == AttachmentKind.Text ? "text/plain" : "image/png";
}
public interface IAttachmentImageNormalizer
{
    Task NormalizeAsync(Stream source, Stream destination, int maxEdge, CancellationToken ct);
}
/// <summary>Fresh local lab scope, not an authenticated customer partition. One instance owns its generated folder.</summary>
public sealed class AttachmentWorkspace
{
    public const int MaximumTextBytes = 64 * 1024, MaximumInputImageBytes = 4 * 1024 * 1024, MaximumOutputBytes = 2 * 1024 * 1024;
    private readonly string directory;
    private readonly string parent;
    private readonly IAttachmentImageNormalizer normalizer;
    private readonly SemaphoreSlim gate = new(1, 1);
    private static readonly SemaphoreSlim CacheCommitGate = new(1, 1);
    private readonly object sync = new();
    private long generation;
    private CancellationTokenSource? pending;
    private AttachmentReceipt? current;
    public long Generation { get { lock (sync) return generation; } }
    public AttachmentReceipt? Current { get { lock (sync) return current; } }
    public bool CleanupUnconfirmed { get; private set; }
    public AttachmentWorkspace(string parent, IAttachmentImageNormalizer normalizer)
    { this.parent = Path.GetFullPath(parent); directory = Path.Combine(this.parent, Guid.NewGuid().ToString("N")); Directory.CreateDirectory(directory); this.normalizer = normalizer; }
    private string FileFor(AttachmentReceipt receipt) => Path.Combine(directory, receipt.Id + receipt.Extension);
    public async Task<AttachmentReceipt> StageAsync(AttachmentKind kind, Func<CancellationToken, Task<Stream>> open,
        long expectedGeneration, CancellationToken ct = default)
    {
        if (kind is not AttachmentKind.Text and not AttachmentKind.Photo) throw new ArgumentException("Unknown attachment kind.");
        await gate.WaitAsync(ct);
        using var linked = CancellationTokenSource.CreateLinkedTokenSource(ct); var token = linked.Token; string? part = null; var cacheHeld = false;
        try
        {
            lock (sync) { Check(expectedGeneration, token); pending = linked; }
            await using var input = await open(token); token.ThrowIfCancellationRequested();
            var bytes = await ReadBoundedAsync(input, kind == AttachmentKind.Text ? MaximumTextBytes : MaximumInputImageBytes, token);
            if (bytes.Length == 0) throw new InvalidDataException("An empty reference is not an attachment.");
            if (kind == AttachmentKind.Text)
            {
                ValidateText(bytes);
            }
            else if (kind == AttachmentKind.Photo)
            {
                _ = ImageEnvelope.Inspect(bytes);
                using var source = new MemoryStream(bytes, writable: false);
                using var output = new BoundedMemoryStream(MaximumOutputBytes);
                await normalizer.NormalizeAsync(source, output, 800, token);
                token.ThrowIfCancellationRequested(); bytes = ImageEnvelope.PixelOnlyPng(output.ToArray());
            }
            else throw new ArgumentException("Unknown attachment kind.");
            lock (sync) Check(expectedGeneration, token);
            await CacheCommitGate.WaitAsync(token); cacheHeld = true;
            lock (sync) Check(expectedGeneration, token);
            if (Directory.EnumerateFiles(parent, "*", SearchOption.AllDirectories).Sum(path => new FileInfo(path).Length) + bytes.Length > 16 * 1024 * 1024)
                throw new IOException("Attachment workspace quota exceeded; clear the lab scope.");
            var receipt = new AttachmentReceipt(Guid.NewGuid().ToString("N"), kind, bytes.Length, Convert.ToHexString(SHA256.HashData(bytes)).ToLowerInvariant());
            part = FileFor(receipt) + ".part"; await File.WriteAllBytesAsync(part, bytes, token);
            AttachmentReceipt? previous;
            lock (sync)
            {
                Check(expectedGeneration, token); File.Move(part, FileFor(receipt)); part = null;
                previous = current; current = receipt;
            }
            try { if (previous is not null) File.Delete(FileFor(previous)); }
            catch { CleanupUnconfirmed = true; }
            return receipt;
        }
        finally
        {
            if (part is not null) { try { File.Delete(part); } catch { CleanupUnconfirmed = true; } }
            lock (sync) pending = null;
            if (cacheHeld) CacheCommitGate.Release();
            gate.Release();
        }
    }
    public async Task<byte[]> ReadCurrentAsync(long expectedGeneration, CancellationToken ct = default)
    {
        await gate.WaitAsync(ct);
        try
        {
            AttachmentReceipt receipt;
            lock (sync) { Check(expectedGeneration, ct); receipt = current ?? throw new InvalidOperationException("No staged attachment."); }
            await using var input = File.OpenRead(FileFor(receipt));
            var bytes = await ReadBoundedAsync(input, receipt.Length, ct);
            if (bytes.Length != receipt.Length || Convert.ToHexString(SHA256.HashData(bytes)).ToLowerInvariant() != receipt.Sha256)
                throw new InvalidDataException("Staged attachment integrity changed.");
            lock (sync) Check(expectedGeneration, ct); return bytes;
        }
        finally { gate.Release(); }
    }
    public async Task ClearAsync(CancellationToken ct = default)
    {
        lock (sync) { generation = checked(generation + 1); current = null; CleanupUnconfirmed = true; pending?.Cancel(); }
        await gate.WaitAsync(ct);
        try { foreach (var file in Directory.EnumerateFiles(directory)) File.Delete(file); CleanupUnconfirmed = false; }
        catch { CleanupUnconfirmed = true; throw; }
        finally { gate.Release(); }
    }
    private void Check(long expected, CancellationToken ct)
    { ct.ThrowIfCancellationRequested(); if (generation != expected) throw new OperationCanceledException("The local attachment scope changed.", ct); }
    internal static void ValidateText(byte[] bytes)
    {
        if (bytes.Length is <= 0 or > MaximumTextBytes) throw new InvalidDataException("Text exceeds policy.");
        var text = new UTF8Encoding(false, true).GetString(bytes);
        if (text.Any(c => char.IsControl(c) && c is not '\r' and not '\n' and not '\t')) throw new InvalidDataException("Unsupported text control characters.");
    }
    internal static async Task<byte[]> ReadBoundedAsync(Stream input, int limit, CancellationToken ct)
    {
        using var output = new MemoryStream(); var buffer = new byte[8192]; int count;
        while ((count = await input.ReadAsync(buffer, ct)) != 0)
        { if (output.Length + count > limit) throw new InvalidDataException("Attachment exceeds its byte limit."); output.Write(buffer, 0, count); }
        return output.ToArray();
    }
}
/// <summary>Bounds native encoder writes, independently of advertised image size.</summary>
public sealed class BoundedMemoryStream(int limit) : MemoryStream
{
    private void Check(int count) { if (Position + count > limit) throw new InvalidDataException("Normalized image exceeds its output byte limit."); }
    public override void Write(byte[] buffer, int offset, int count) { Check(count); base.Write(buffer, offset, count); }
    public override void Write(ReadOnlySpan<byte> buffer) { Check(buffer.Length); base.Write(buffer); }
    public override void WriteByte(byte value) { Check(1); base.WriteByte(value); }
    public override Task WriteAsync(byte[] buffer, int offset, int count, CancellationToken ct) { Check(count); return base.WriteAsync(buffer, offset, count, ct); }
    public override ValueTask WriteAsync(ReadOnlyMemory<byte> buffer, CancellationToken ct = default) { Check(buffer.Length); return base.WriteAsync(buffer, ct); }
    public override void SetLength(long value) { if (value > limit) throw new InvalidDataException("Output length exceeds policy."); base.SetLength(value); }
}

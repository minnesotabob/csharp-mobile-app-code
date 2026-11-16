using System.Security.Cryptography;
using System.Text.RegularExpressions;

namespace GnazzosWorld.Core.Attachments;

/// <summary>One instance owns the narrow share root. Copies may outlive the chooser; external recipients cannot be revoked.</summary>
public sealed class AttachmentExportStore(string root, TimeProvider clock)
{
    private readonly string directory = Path.GetFullPath(root);
    private readonly SemaphoreSlim gate = new(1, 1);
    private static bool Owned(string path) => Regex.IsMatch(Path.GetFileName(path), @"\A[a-f0-9]{32}\.(txt|png)(\.part)?\z", RegexOptions.CultureInvariant);
    public async Task<string> CreateAsync(AttachmentReceipt receipt, byte[] data, CancellationToken ct = default)
    {
        if (data.Length != receipt.Length || data.Length == 0 || data.Length > AttachmentWorkspace.MaximumOutputBytes ||
            Convert.ToHexString(SHA256.HashData(data)).ToLowerInvariant() != receipt.Sha256 || receipt.Kind is not AttachmentKind.Text and not AttachmentKind.Photo)
            throw new InvalidDataException("Export must match a verified staged attachment.");
        if (receipt.Kind == AttachmentKind.Text) AttachmentWorkspace.ValidateText(data);
        else if (!ImageEnvelope.PixelOnlyPng(data).AsSpan().SequenceEqual(data)) throw new InvalidDataException("Export contains ancillary PNG metadata.");
        await gate.WaitAsync(ct); string? part = null;
        try
        {
            Directory.CreateDirectory(directory);
            foreach (var old in Directory.EnumerateFiles(directory).Where(Owned))
                if (File.GetLastWriteTimeUtc(old) <= clock.GetUtcNow().AddHours(-24).UtcDateTime) File.Delete(old);
            var files = Directory.EnumerateFiles(directory).ToArray();
            if (files.Length >= 4 || files.Sum(x => new FileInfo(x).Length) + data.Length > 8 * 1024 * 1024)
                throw new IOException("Share-copy budget exceeded. Remove local exported copies explicitly.");
            var final = Path.Combine(directory, Guid.NewGuid().ToString("N") + receipt.Extension); part = final + ".part";
            await File.WriteAllBytesAsync(part, data, ct); ct.ThrowIfCancellationRequested();
            File.Move(part, final); part = null; File.SetLastWriteTimeUtc(final, clock.GetUtcNow().UtcDateTime);
            return final;
        }
        finally { if (part is not null) File.Delete(part); gate.Release(); }
    }
    public async Task ClearAsync(CancellationToken ct = default)
    {
        await gate.WaitAsync(ct);
        try { if (Directory.Exists(directory)) foreach (var file in Directory.EnumerateFiles(directory).Where(Owned)) File.Delete(file); }
        finally { gate.Release(); }
    }
}

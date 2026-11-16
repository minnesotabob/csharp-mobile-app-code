using GnazzosWorld.Core.Attachments;
using Microsoft.Maui.Graphics;
using Microsoft.Maui.Graphics.Platform;

namespace GnazzosWorld.App.Capabilities;

public sealed class NativeImageNormalizer : IAttachmentImageNormalizer
{
    public Task NormalizeAsync(Stream source, Stream destination, int maxEdge, CancellationToken ct) => Task.Run(() =>
    {
        ct.ThrowIfCancellationRequested();
        using var decoded = PlatformImage.FromStream(source) ?? throw new InvalidDataException("The native decoder rejected the image.");
        if (!float.IsFinite(decoded.Width) || !float.IsFinite(decoded.Height) || decoded.Width <= 0 || decoded.Height <= 0 ||
            decoded.Width > 4096 || decoded.Height > 4096 || decoded.Width * decoded.Height > 4000000)
            throw new InvalidDataException("Decoded dimensions exceed policy.");
        if (decoded.Width <= maxEdge && decoded.Height <= maxEdge) decoded.Save(destination, ImageFormat.Png);
        else
        {
            using var resized = decoded.Downsize(maxEdge, disposeOriginal: false);
            resized.Save(destination, ImageFormat.Png);
        }
        ct.ThrowIfCancellationRequested(); // Native decode/encode cannot be preempted by this token.
    }, ct);
}

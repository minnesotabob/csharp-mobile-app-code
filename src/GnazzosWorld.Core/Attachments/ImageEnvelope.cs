using System.Buffers.Binary;

namespace GnazzosWorld.Core.Attachments;

public sealed record ImageShape(string Format, int Width, int Height);
/// <summary>Resource preflight, not a full JPEG/PNG decoder. Native decoding is another trust boundary.</summary>
public static class ImageEnvelope
{
    private static ReadOnlySpan<byte> PngSignature => [137, 80, 78, 71, 13, 10, 26, 10];
    private static int U32(ReadOnlySpan<byte> value)
    {
        var number = BinaryPrimitives.ReadUInt32BigEndian(value);
        return number <= int.MaxValue ? (int)number : throw new InvalidDataException("Image field exceeds its bound.");
    }
    public static ImageShape Inspect(ReadOnlySpan<byte> data, int maxEdge = 4096, long maxPixels = 4000000)
    {
        int width, height; string format;
        if (data.Length >= 33 && data[..8].SequenceEqual(PngSignature))
        {
            if (U32(data.Slice(8, 4)) != 13 || !data.Slice(12, 4).SequenceEqual("IHDR"u8)) throw new InvalidDataException("Expected PNG IHDR.");
            width = U32(data.Slice(16, 4)); height = U32(data.Slice(20, 4)); format = "png";
        }
        else if (data.Length >= 4 && data[0] == 255 && data[1] == 216)
        {
            var position = 2; width = height = 0; format = "jpeg";
            while (position < data.Length)
            {
                if (data[position++] != 255) throw new InvalidDataException("Invalid JPEG marker boundary.");
                while (position < data.Length && data[position] == 255) position++;
                if (position >= data.Length) break;
                var marker = data[position++];
                if (marker is 0 or 216 or 217 or 218 || marker is >= 208 and <= 215) break;
                if (position + 2 > data.Length) break;
                var length = BinaryPrimitives.ReadUInt16BigEndian(data.Slice(position, 2));
                if (length < 2 || length > data.Length - position) break;
                if (marker is 192 or 193 or 194 or 195 or 197 or 198 or 199 or 201 or 202 or 203 or 205 or 206 or 207)
                {
                    if (length < 8) throw new InvalidDataException("Truncated JPEG frame.");
                    height = BinaryPrimitives.ReadUInt16BigEndian(data.Slice(position + 3, 2));
                    width = BinaryPrimitives.ReadUInt16BigEndian(data.Slice(position + 5, 2)); break;
                }
                position += length;
            }
        }
        else throw new InvalidDataException("Only bounded PNG/JPEG input is supported.");
        if (width <= 0 || height <= 0 || width > maxEdge || height > maxEdge || (long)width * height > maxPixels)
            throw new InvalidDataException("Image dimensions exceed the input policy.");
        return new(format, width, height);
    }
    /// <summary>Validate native PNG framing/CRC and retain only RGB/RGBA pixel chunks.
    /// Removes EXIF/text/profile/other ancillary chunks; not proof that pixels contain no private information.</summary>
    public static byte[] PixelOnlyPng(ReadOnlySpan<byte> data, int maxEdge = 800)
    {
        var shape = Inspect(data, maxEdge, (long)maxEdge * maxEdge);
        if (shape.Format != "png") throw new InvalidDataException("Normalizer must output PNG.");
        using var output = new MemoryStream(); output.Write(PngSignature);
        var position = 8; var ihdr = false; var idat = false; var ended = false;
        while (position < data.Length)
        {
            if (data.Length - position < 12) throw new InvalidDataException("Truncated PNG chunk.");
            var length = U32(data.Slice(position, 4));
            if (length > data.Length - position - 12) throw new InvalidDataException("PNG chunk exceeds the envelope.");
            var type = data.Slice(position + 4, 4); var body = data.Slice(position + 8, length);
            var crc = BinaryPrimitives.ReadUInt32BigEndian(data.Slice(position + 8 + length, 4));
            if (Crc(data.Slice(position + 4, length + 4)) != crc) throw new InvalidDataException("PNG chunk CRC mismatch.");
            if (!ihdr && !type.SequenceEqual("IHDR"u8)) throw new InvalidDataException("IHDR must be first.");
            if (type.SequenceEqual("IHDR"u8))
            {
                if (ihdr || length != 13 || body[8] != 8 || body[9] is not 2 and not 6 || body[10] != 0 || body[11] != 0 || body[12] != 0)
                    throw new InvalidDataException("Unsupported normalized PNG header.");
                ihdr = true;
            }
            var keep = type.SequenceEqual("IHDR"u8) || type.SequenceEqual("IDAT"u8) || type.SequenceEqual("IEND"u8);
            if (type.SequenceEqual("tRNS"u8)) throw new InvalidDataException("Normalizer must encode transparency as RGBA pixels.");
            if (!keep && (type[0] & 32) == 0) throw new InvalidDataException("Unsupported critical PNG chunk.");
            if (type.SequenceEqual("IDAT"u8)) idat = true;
            if (type.SequenceEqual("IEND"u8))
            { if (length != 0 || !idat || position + 12 != data.Length) throw new InvalidDataException("Invalid PNG ending."); ended = true; }
            if (keep) output.Write(data.Slice(position, length + 12));
            position += length + 12;
        }
        if (!ended) throw new InvalidDataException("Missing PNG ending.");
        return output.ToArray();
    }
    private static uint Crc(ReadOnlySpan<byte> data)
    {
        var crc = uint.MaxValue;
        foreach (var value in data)
        {
            crc ^= value;
            for (var bit = 0; bit < 8; bit++) crc = (crc >> 1) ^ ((crc & 1) != 0 ? 0xedb88320u : 0);
        }
        return ~crc;
    }
}

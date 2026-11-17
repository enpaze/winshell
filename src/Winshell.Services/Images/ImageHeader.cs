using System.Buffers.Binary;

namespace Winshell;

internal static class ImageHeader
{
    public static (string Extension, uint Width, uint Height)? Read(ReadOnlySpan<byte> data)
    {
        var (extension, size) = data switch
        {
            [0x89, (byte)'P', (byte)'N', (byte)'G', 0x0D, 0x0A, 0x1A, 0x0A, ..] => (".png", Png(data)),
            [0xFF, 0xD8, 0xFF, ..] => (".jpg", Jpeg(data)),
            [(byte)'G', (byte)'I', (byte)'F', (byte)'8', (byte)'7' or (byte)'9', (byte)'a', ..] => (".gif", Gif(data)),
            [(byte)'B', (byte)'M', ..] => (".bmp", Bmp(data)),
            [
                (byte)'R', (byte)'I', (byte)'F', (byte)'F', _, _, _, _, (byte)'W', (byte)'E', (byte)'B', (byte)'P', ..
            ] => (".webp", WebP(data)),
            _ => ("", null)
        };
        return size is var (width, height) ? (extension, width, height) : null;
    }

    private static (uint, uint)? Png(ReadOnlySpan<byte> data)
    {
        return data.Length >= 24 && data[12..16].SequenceEqual("IHDR"u8)
            ? (BinaryPrimitives.ReadUInt32BigEndian(data[16..]), BinaryPrimitives.ReadUInt32BigEndian(data[20..]))
            : null;
    }

    private static (uint, uint)? Jpeg(ReadOnlySpan<byte> data)
    {
        const byte fill = 0xFF;
        const byte startOfScan = 0xDA;
        const byte endOfImage = 0xD9;
        var at = 2;
        while (at + 1 < data.Length)
        {
            if (data[at] != fill || data[at + 1] is fill or 0)
            {
                at++;
                continue;
            }

            var marker = data[at + 1];
            at += 2;
            if (marker is startOfScan or endOfImage)
                return null;
            if (marker is >= 0xD0 and <= 0xD8 or 0x01)
                continue;
            if (at + 2 > data.Length)
                return null;
            if (marker is >= 0xC0 and <= 0xCF and not (0xC4 or 0xC8 or 0xCC))
                return at + 7 <= data.Length
                    ? (BinaryPrimitives.ReadUInt16BigEndian(data[(at + 5)..]),
                        BinaryPrimitives.ReadUInt16BigEndian(data[(at + 3)..]))
                    : null;
            at += BinaryPrimitives.ReadUInt16BigEndian(data[at..]);
        }

        return null;
    }

    private static (uint, uint)? Gif(ReadOnlySpan<byte> data)
    {
        return data.Length >= 10
            ? (BinaryPrimitives.ReadUInt16LittleEndian(data[6..]), BinaryPrimitives.ReadUInt16LittleEndian(data[8..]))
            : null;
    }

    private static (uint, uint)? Bmp(ReadOnlySpan<byte> data)
    {
        if (data.Length < 18)
            return null;
        var header = BinaryPrimitives.ReadUInt32LittleEndian(data[14..]);
        if (header == 12)
            return data.Length >= 22
                ? (BinaryPrimitives.ReadUInt16LittleEndian(data[18..]),
                    BinaryPrimitives.ReadUInt16LittleEndian(data[20..]))
                : null;
        if (header < 16 || data.Length < 26)
            return null;
        var width = BinaryPrimitives.ReadInt32LittleEndian(data[18..]);
        var height = (long)BinaryPrimitives.ReadInt32LittleEndian(data[22..]);
        return width > 0 ? ((uint)width, (uint)Math.Abs(height)) : null;
    }

    private static (uint, uint)? WebP(ReadOnlySpan<byte> data)
    {
        if (data.Length < 30)
            return null;
        var chunk = data[12..16];
        var payload = data[20..];
        if (chunk.SequenceEqual("VP8 "u8))
            return payload[3..6].SequenceEqual<byte>([0x9D, 0x01, 0x2A])
                ? ((uint)(BinaryPrimitives.ReadUInt16LittleEndian(payload[6..]) & 0x3FFF),
                    (uint)(BinaryPrimitives.ReadUInt16LittleEndian(payload[8..]) & 0x3FFF))
                : null;
        if (chunk.SequenceEqual("VP8L"u8))
        {
            var sizes = BinaryPrimitives.ReadUInt32LittleEndian(payload[1..]);
            return payload[0] == 0x2F ? ((sizes & 0x3FFF) + 1, ((sizes >> 14) & 0x3FFF) + 1) : null;
        }

        return chunk.SequenceEqual("VP8X"u8) ? (UInt24(payload[4..]) + 1, UInt24(payload[7..]) + 1) : null;
    }

    private static uint UInt24(ReadOnlySpan<byte> data)
    {
        return (uint)(data[0] | (data[1] << 8) | (data[2] << 16));
    }
}

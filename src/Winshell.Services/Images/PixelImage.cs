using Windows.Graphics.Imaging;
using Windows.Storage.Streams;

namespace Winshell;

public sealed class PixelImage
{
    private readonly byte[] _bgra;

    public PixelImage(int width, int height, byte[] bgra, bool premultiplied = false)
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(width);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(height);
        ArgumentNullException.ThrowIfNull(bgra);
        if (bgra.Length != checked(width * height * 4))
            throw new ArgumentException("The pixels must be width times height times 4 bytes.", nameof(bgra));
        Width = width;
        Height = height;
        Premultiplied = premultiplied;
        _bgra = bgra;
    }

    public int Width { get; }

    public int Height { get; }

    public bool Premultiplied { get; }

    public ReadOnlySpan<byte> Bgra => _bgra;

    public byte[] EncodePng()
    {
        using var output = new InMemoryRandomAccessStream();
#pragma warning disable VSTHRD002
        var encoder = BitmapEncoder.CreateAsync(BitmapEncoder.PngEncoderId, output).AsTask().GetAwaiter().GetResult();
        encoder.SetPixelData(BitmapPixelFormat.Bgra8, BitmapAlphaMode.Straight, (uint)Width, (uint)Height, 96, 96,
            StraightBgra());
        encoder.FlushAsync().AsTask().GetAwaiter().GetResult();
#pragma warning restore VSTHRD002
        using var encoded = new MemoryStream();
        using (var input = output.GetInputStreamAt(0).AsStreamForRead())
        {
            input.CopyTo(encoded);
        }

        return encoded.ToArray();
    }

    internal byte[] StraightBgra()
    {
        if (!Premultiplied)
            return _bgra;
        var straight = (byte[])_bgra.Clone();
        for (var i = 0; i < straight.Length; i += 4)
        {
            var alpha = straight[i + 3];
            if (alpha is 0 or 255)
                continue;
            for (var channel = i; channel < i + 3; channel++)
                straight[channel] = (byte)Math.Min(255, (straight[channel] * 255 + alpha / 2) / alpha);
        }

        return straight;
    }
}

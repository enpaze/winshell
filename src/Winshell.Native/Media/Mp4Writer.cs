using Windows.Win32;
using Windows.Win32.Media.MediaFoundation;

namespace Winshell.Native;

/// <summary>
/// An H.264 MP4 file at a constant frame rate, written by Media Foundation's sink writer from top-down BGRA frames of
/// one size. Frame <c>k</c> starts at <c>k / rate</c> seconds. One caller at a time writes, then disposes. A file that
/// <see cref="Finish"/> did not end is not playable.
/// </summary>
internal sealed unsafe class Mp4Writer : IDisposable
{
    // Media Foundation counts time in 100 ns units.
    private const long Second = 10_000_000;

    private readonly int _width;
    private readonly int _height;
    private readonly int _rate;
    private readonly uint _stream;
    private IMFSinkWriter* _writer;
    private bool _started;
    private long _frames;

    /// <summary>Creates or replaces <paramref name="path"/>, which must end in <c>.mp4</c>.</summary>
    public Mp4Writer(string path, int width, int height, int rate)
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(width);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(height);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(rate);
        _width = width;
        _height = height;
        _rate = rate;
        PInvoke.MFStartup(PInvoke.MF_VERSION, PInvoke.MFSTARTUP_LITE).ThrowOnFailure();
        _started = true;
        try
        {
            _stream = Open(path);
        }
        catch
        {
            Dispose();
            throw;
        }
    }

    private uint Open(string path)
    {
        IMFAttributes* attributes = null;
        IMFMediaType* output = null;
        IMFMediaType* input = null;
        try
        {
            PInvoke.MFCreateAttributes(&attributes, 2).ThrowOnFailure();
            // Hardware encoders are off unless allowed. Throttling would hold each write back to the encoder's pace; the
            // caller already writes off the capture thread.
            attributes->SetUINT32(PInvoke.MF_READWRITE_ENABLE_HARDWARE_TRANSFORMS, 1);
            attributes->SetUINT32(PInvoke.MF_SINK_WRITER_DISABLE_THROTTLING, 1);
            IMFSinkWriter* writer = null;
            PInvoke.MFCreateSinkWriterFromURL(path, null, attributes, &writer).ThrowOnFailure();
            _writer = writer;

            output = VideoType(PInvoke.MFVideoFormat_H264);
            output->SetUINT32(PInvoke.MF_MT_AVG_BITRATE, Bitrate(_width, _height, _rate));
            uint stream;
            _writer->AddStream(output, &stream);

            // The H.264 encoder takes only YUV. The sink writer converts BGRA on its way in, and a positive stride
            // says the rows are top-down, as a capture's are.
            input = VideoType(PInvoke.MFVideoFormat_RGB32);
            input->SetUINT32(PInvoke.MF_MT_DEFAULT_STRIDE, (uint)(_width * 4));
            _writer->SetInputMediaType(stream, input, null);
            _writer->BeginWriting();
            return stream;
        }
        finally
        {
            Release(attributes);
            Release(output);
            Release(input);
        }
    }

    /// <summary>
    /// Shows <paramref name="bgra"/>, a frame of the writer's size, for the next <paramref name="count"/> frames of the
    /// file.
    /// </summary>
    public void Write(ReadOnlySpan<byte> bgra, int count)
    {
        ObjectDisposedException.ThrowIf(_writer is null, this);
        var length = checked(_width * _height * 4);
        if (bgra.Length != length)
            throw new ArgumentException("A frame must be the writer's width times height times 4 bytes.", nameof(bgra));
        if (count <= 0)
            return;
        IMFMediaBuffer* buffer = null;
        try
        {
            PInvoke.MFCreateMemoryBuffer((uint)length, &buffer).ThrowOnFailure();
            byte* data;
            buffer->Lock(&data, null, null);
            bgra.CopyTo(new Span<byte>(data, length));
            buffer->Unlock();
            buffer->SetCurrentLength((uint)length);
            // A repeated frame is a new sample over the same pixels. The writer may still hold the last sample, so
            // nothing it was given changes.
            for (var i = 0; i < count; i++, _frames++)
                WriteSample(buffer, _frames);
        }
        finally
        {
            Release(buffer);
        }
    }

    /// <summary>Ends the file, which makes it playable. Blocks until the encoder has written every frame.</summary>
    public void Finish()
    {
        ObjectDisposedException.ThrowIf(_writer is null, this);
        _writer->Finalize();
    }

    public void Dispose()
    {
        Release(_writer);
        _writer = null;
        if (_started)
            PInvoke.MFShutdown();
        _started = false;
    }

    private void WriteSample(IMFMediaBuffer* buffer, long frame)
    {
        IMFSample* sample = null;
        try
        {
            PInvoke.MFCreateSample(&sample).ThrowOnFailure();
            sample->AddBuffer(buffer);
            var start = frame * Second / _rate;
            sample->SetSampleTime(start);
            sample->SetSampleDuration((frame + 1) * Second / _rate - start);
            _writer->WriteSample(_stream, sample);
        }
        finally
        {
            Release(sample);
        }
    }

    private IMFMediaType* VideoType(Guid subtype)
    {
        IMFMediaType* type = null;
        try
        {
            PInvoke.MFCreateMediaType(&type).ThrowOnFailure();
            type->SetGUID(PInvoke.MF_MT_MAJOR_TYPE, PInvoke.MFMediaType_Video);
            type->SetGUID(PInvoke.MF_MT_SUBTYPE, subtype);
            type->SetUINT32(PInvoke.MF_MT_INTERLACE_MODE, (uint)MFVideoInterlaceMode.MFVideoInterlace_Progressive);
            // Media Foundation packs a pair into one 64-bit value, the first half high. MFSetAttributeSize is a header
            // helper, not an export.
            type->SetUINT64(PInvoke.MF_MT_FRAME_SIZE, Pair((uint)_width, (uint)_height));
            type->SetUINT64(PInvoke.MF_MT_FRAME_RATE, Pair((uint)_rate, 1));
            type->SetUINT64(PInvoke.MF_MT_PIXEL_ASPECT_RATIO, Pair(1, 1));
            return type;
        }
        catch
        {
            Release(type);
            throw;
        }
    }

    private static ulong Pair(uint high, uint low)
    {
        return ((ulong)high << 32) | low;
    }

    /// <summary>About a tenth of a bit a pixel, which keeps text sharp, from 1 to 50 Mbit/s.</summary>
    private static uint Bitrate(int width, int height, int rate)
    {
        return (uint)Math.Clamp((long)width * height * rate / 10, 1_000_000, 50_000_000);
    }

    private static void Release(IMFAttributes* pointer)
    {
        if (pointer is not null)
            pointer->Release();
    }

    private static void Release(IMFMediaType* pointer)
    {
        if (pointer is not null)
            pointer->Release();
    }

    private static void Release(IMFMediaBuffer* pointer)
    {
        if (pointer is not null)
            pointer->Release();
    }

    private static void Release(IMFSample* pointer)
    {
        if (pointer is not null)
            pointer->Release();
    }

    private static void Release(IMFSinkWriter* pointer)
    {
        if (pointer is not null)
            pointer->Release();
    }
}

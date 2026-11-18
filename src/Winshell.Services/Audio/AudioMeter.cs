namespace Winshell.Audio;

internal interface IAudioMeter
{
    float[] ReadPeaks();
}

internal sealed class WindowsAudioMeter(Native.IAudioMeterInformation meter) : IAudioMeter
{
    public float[] ReadPeaks()
    {
        meter.GetMeteringChannelCount(out var count);
        var peaks = new float[count];
        return count > 0 && meter.GetChannelsPeakValues(count, peaks) == 0 ? peaks : [];
    }
}

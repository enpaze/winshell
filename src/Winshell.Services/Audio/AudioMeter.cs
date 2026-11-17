namespace Winshell.Audio;

internal interface IAudioMeterInformation
{
    float[] ReadPeaks();
}

internal sealed class WindowsAudioMeter(Native.IAudioMeterInformation meter) : IAudioMeterInformation
{
    public float[] ReadPeaks()
    {
        meter.GetMeteringChannelCount(out var count);
        var peaks = new float[count];
        return count > 0 && meter.GetChannelsPeakValues(count, peaks) == 0 ? peaks : [];
    }
}

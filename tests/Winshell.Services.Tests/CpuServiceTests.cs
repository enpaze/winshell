using System.ComponentModel;
using Winshell.Cpu;

namespace Winshell.Services.Tests;

public sealed class CpuServiceTests
{
    private static readonly TimeSpan Interval = TimeSpan.FromSeconds(2);

    private static void Ignore(object? sender, PropertyChangedEventArgs e)
    {
    }

    [Fact]
    public void Usage_is_the_busy_share_of_the_time_since_the_last_reading()
    {
        var loop = new ManualEventLoop();
        var backend = new FakeCpuBackend(
            new CpuTimes(9000, 12000, 3000),
            new CpuTimes(9300, 12500, 3500),
            new CpuTimes(10050, 13400, 3600));
        using var cpu = new CpuService(loop, backend);
        List<(string? Name, double Usage)> heard = [];
        cpu.PropertyChanged += (_, e) => heard.Add((e.PropertyName, cpu.Usage));
        Assert.Equal(0, cpu.Usage);

        loop.Advance(Interval);
        loop.Advance(Interval);

        Assert.Equal([(nameof(CpuService.Usage), 70.0), (nameof(CpuService.Usage), 25.0)], heard);
    }

    [Fact]
    public void A_failed_read_starts_over_from_the_next_reading()
    {
        var loop = new ManualEventLoop();
        var backend = new FakeCpuBackend(
            new CpuTimes(0, 1000, 0),
            null,
            new CpuTimes(5000, 10000, 0),
            new CpuTimes(5500, 11000, 0));
        using var cpu = new CpuService(loop, backend);
        cpu.PropertyChanged += Ignore;

        loop.Advance(Interval * 2);
        Assert.Equal(0, cpu.Usage);

        loop.Advance(Interval);
        Assert.Equal(50, cpu.Usage);
    }

    [Fact]
    public void Windows_is_read_every_2_seconds_only_while_something_listens()
    {
        var loop = new ManualEventLoop();
        var backend = new FakeCpuBackend();
        using var cpu = new CpuService(loop, backend);
        PropertyChangedEventHandler second = Ignore;

        loop.Advance(Interval * 5);
        Assert.Equal(0, backend.Reads);
        Assert.Null(loop.NextDue);

        cpu.PropertyChanged += Ignore;
        cpu.PropertyChanged += second;
        loop.Advance(Interval * 2);
        Assert.Equal(3, backend.Reads);

        cpu.PropertyChanged -= second;
        loop.Advance(Interval);
        Assert.Equal(4, backend.Reads);
        Assert.Equal(50, cpu.Usage);

        cpu.PropertyChanged -= Ignore;
        loop.Advance(Interval * 5);
        Assert.Equal(4, backend.Reads);
        Assert.Null(loop.NextDue);
        Assert.Equal(0, cpu.Usage);

        cpu.PropertyChanged += Ignore;
        Assert.Equal(0, cpu.Usage);
        loop.Advance(Interval);
        Assert.Equal(6, backend.Reads);
        Assert.Equal(50, cpu.Usage);
    }

    private sealed class FakeCpuBackend(params CpuTimes?[] readings) : ICpuBackend
    {
        public int Reads { get; private set; }

        public CpuTimes? Read()
        {
            var n = Reads++;
            if (n < readings.Length)
                return readings[n];
            var ticks = (ulong)n * 1000;
            return new CpuTimes(ticks / 2, ticks, 0);
        }
    }
}

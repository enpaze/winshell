using Windows.Devices.Radios;

namespace Winshell.Services.Tests;

internal sealed class FakeRadio(RadioKind kind, RadioState state) : IDeviceRadio
{
    public RadioKind Kind { get; } = kind;
    public RadioState State { get; private set; } = state;
    public RadioAccessStatus Access { get; set; } = RadioAccessStatus.Allowed;
    public RadioAccessStatus Result { get; set; } = RadioAccessStatus.Allowed;
    public bool Reach { get; set; } = true;
    public Exception? Failure { get; set; }
    public TaskCompletionSource<RadioAccessStatus>? Gate { get; init; }
    public int AccessRequests { get; private set; }
    public int Sets { get; private set; }

    public Task<RadioAccessStatus> RequestAccessAsync()
    {
        AccessRequests++;
        return Task.FromResult(Access);
    }

    public async Task<RadioAccessStatus> SetStateAsync(RadioState next)
    {
        Sets++;
        if (Failure is { } failure)
            throw failure;
        var result = Gate is { } gate ? await gate.Task.ConfigureAwait(false) : Result;
        if (result == RadioAccessStatus.Allowed && Reach)
            State = next;
        return result;
    }
}

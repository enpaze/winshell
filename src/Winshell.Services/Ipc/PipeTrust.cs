using System.IO.Pipes;
using System.Security.Principal;
using Microsoft.Win32.SafeHandles;
using Windows.Win32.Foundation;
using Winshell.Native;

namespace Winshell;

internal static class PipeTrust
{
    private const string MediumLabel = "S:(ML;;NW;;;ME)";

    internal static bool AtLeast(uint? actor, uint? floor)
    {
        return actor >= floor && floor > 0;
    }

    internal static SafePipeHandle OpenServer(string pipe, bool first)
    {
        var sid = WindowsIdentity.GetCurrent().User?.Value
                  ?? throw new IOException("The control pipe could not be labeled.");
        var path = @"\\.\pipe\" + pipe;
        var labeled =
            NamedPipes.CreateServer(path, first, $"O:{sid}D:(A;;GA;;;{sid}){MediumLabel}", out var error);
        if (labeled is not null && IsMedium(labeled))
            return labeled;
        labeled?.Dispose();
        if (first && error == (int)WIN32_ERROR.ERROR_ACCESS_DENIED)
            throw new PipeTakenException();
        var handle = NamedPipes.CreateServer(path, first, $"O:{sid}D:(A;;GA;;;{sid})", out error);
        if (handle is null)
            throw error == (int)WIN32_ERROR.ERROR_ACCESS_DENIED
                ? new PipeTakenException()
                : new IOException($"The control pipe could not be created ({error}).");
        try
        {
            if (!IsMedium(handle))
                SetMediumLabel(handle);
            return handle;
        }
        catch (IOException) when (!first)
        {
            if (IsMedium(handle))
                return handle;
            handle.Dispose();
            throw;
        }
        catch
        {
            handle.Dispose();
            throw;
        }
    }

    private static bool IsMedium(SafePipeHandle pipe)
    {
        var label = NamedPipes.MandatoryLabel(pipe);
        return label.Contains("ME", StringComparison.Ordinal) && label.Contains("NW", StringComparison.Ordinal);
    }

    private static void SetMediumLabel(SafePipeHandle pipe)
    {
        var error = NamedPipes.SetMandatoryLabel(pipe, MediumLabel);
        if (error != 0)
            throw new IOException($"The control pipe could not be labeled ({error}).");
    }

    internal static string? ServerRefusal(PipeStream pipe)
    {
        var user = WindowsIdentity.GetCurrent().User;
        if (user is null || !user.Equals(pipe.GetAccessControl().GetOwner(typeof(SecurityIdentifier))))
            return "The control pipe belongs to another user.";
        if (!PInvoke.GetNamedPipeServerProcessId(new HANDLE(pipe.SafePipeHandle.DangerousGetHandle()), out var pid))
            return "The shell behind the control pipe could not be identified.";
        if (!AtLeast(ProcessIntegrity.TryRead(pid), ProcessIntegrity.TryRead((uint)Environment.ProcessId)))
            return "The shell runs at a lower integrity level than this command. Run the command without elevation.";
        return null;
    }

    internal static string? ClientRefusal(SafePipeHandle pipe)
    {
        if (NamedPipes.ClientProcessId(pipe, out var error) is not { } pid)
            return $"The shell could not identify the command's process ({error}).";
        if (ProcessIntegrity.TryRead(pid) is not { } client ||
            ProcessIntegrity.TryRead((uint)Environment.ProcessId) is not { } floor)
            return $"The shell could not read the integrity level of process {pid}.";
        if (!AtLeast(client, floor))
            return
                $"The shell runs at a higher integrity level (0x{floor:X}) than process {pid} (0x{client:X}). Run the command elevated.";
        return null;
    }
}

internal sealed class PipeTakenException() : IOException("The control pipe is already taken.");

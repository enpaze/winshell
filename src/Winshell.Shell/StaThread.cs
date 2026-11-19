using System.Runtime.ExceptionServices;

namespace Winshell;

internal static class StaThread
{
    public static void Run(Action body)
    {
        if (Thread.CurrentThread.GetApartmentState() == ApartmentState.STA)
        {
            body();
            return;
        }

        ExceptionDispatchInfo? failed = null;
        var thread = new Thread(() =>
        {
            try
            {
                body();
            }
            catch (Exception e)
            {
                failed = ExceptionDispatchInfo.Capture(e);
            }
        });
        thread.SetApartmentState(ApartmentState.STA);
        thread.Start();
        thread.Join();
        failed?.Throw();
    }
}

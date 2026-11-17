namespace Winshell;

internal static class OwnThread
{
    public static Task<T> Run<T>(Func<T> call)
    {
        return Task.Factory.StartNew(call, CancellationToken.None,
            TaskCreationOptions.LongRunning | TaskCreationOptions.DenyChildAttach, TaskScheduler.Default);
    }

    public static Task Run(Action call)
    {
        return Run(() =>
        {
            call();
            return false;
        });
    }
}

using System.Collections.Concurrent;
using Microsoft.UI.Reactor.Hosting;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Automation;

namespace Winshell.ShellTests;

internal static class ShellFixture
{
    private static readonly TaskCompletionSource<Shell> Ready = new(TaskCreationOptions.RunContinuationsAsynchronously);
    private static readonly Lazy<Thread> UiThread = new(StartThread);

    private static readonly SemaphoreSlim OneAction = new(1, 1);
    private static readonly AsyncLocal<int> Depth = new();

    private static readonly ConcurrentDictionary<TestContext, List<string>> Left = new();

    private static Thread StartThread()
    {
        Environment.SetEnvironmentVariable("WINSHELL_LOCAL_DIR",
            TempFolder.NewPath());
        Environment.SetEnvironmentVariable("WINSHELL_SHELLS_DIR",
            TempFolder.NewPath());
        var thread = new Thread(() => Shell.Run(Ready.SetResult))
        {
            IsBackground = true,
            Name = "WinUI"
        };
        thread.SetApartmentState(ApartmentState.STA);
        thread.Start();
        return thread;
    }

    public static Task<Shell> Current()
    {
        _ = UiThread.Value;
        return Ready.Task;
    }

    public static Task<T> On<T>(Func<Shell, Task<T>> action)
    {
        return Gate(async () =>
            await OnThread(await Current().WaitAsync(CurrentTest.Cancellation), action));
    }

    public static Task Hold(Func<Task> action)
    {
        return Gate(async () =>
        {
            await action();
            return true;
        });
    }

    private static async Task<T> Gate<T>(Func<Task<T>> body)
    {
        if (Depth.Value > 0)
            return await body();
        await OneAction.WaitAsync(CurrentTest.Cancellation);
        Depth.Value = 1;
        try
        {
            var shell = await Current().WaitAsync(CurrentTest.Cancellation);
            var before = await OnThread(shell, s => Task.FromResult(s.Platform.Windows.ToList()));
            try
            {
                return await body();
            }
            finally
            {
                await CloseLeft(shell, before);
                await ScreensRead(shell);
            }
        }
        finally
        {
            Depth.Value = 0;
            OneAction.Release();
        }
    }

    public static Task<T> On<T>(Func<Shell, T> action)
    {
        return On(shell => Task.FromResult(action(shell)));
    }

    private static async Task CloseLeft(Shell shell, List<HostWindow> before)
    {
        try
        {
            var left = await OnThread(shell, s =>
            {
                var opened = s.Platform.Windows.Except(before).ToList();
                var names = opened.Select(Describe).ToList();
                foreach (var window in opened)
                    window.Client.Dispose();
                return Task.FromResult(names);
            });
            if (left.Count > 0 && CurrentTest.Context is { } test)
                Left.AddOrUpdate(test, left, (_, earlier) => [.. earlier, .. left]);
        }
        catch (OperationCanceledException)
        {
        }
    }

    private static async Task ScreensRead(Shell shell)
    {
        try
        {
            await Wait.UntilAsync(() => OnThread(shell, s => Task.FromResult(!s.Platform.ScreenReadPending)),
                "the screen read the action asked for to finish before the next action starts");
        }
        catch (OperationCanceledException)
        {
        }
    }

    private static async Task<T> OnThread<T>(Shell shell, Func<Shell, Task<T>> action)
    {
        var done = new TaskCompletionSource<T>(TaskCreationOptions.RunContinuationsAsynchronously);
        shell.Loop.Post(() => _ = Run(shell, action, done));
        return await done.Task.WaitAsync(CurrentTest.Cancellation);
    }

    private static async Task Run<T>(Shell shell, Func<Shell, Task<T>> action, TaskCompletionSource<T> done)
    {
        try
        {
            done.SetResult(await action(shell));
        }
#pragma warning disable CA1031
        catch (Exception e)
#pragma warning restore CA1031
        {
            done.SetException(e);
        }
    }

    public static async Task LaidOut(FrameworkElement element)
    {
        if (element.ActualWidth <= 0)
        {
            var sized = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
            element.SizeChanged += (_, _) => sized.TrySetResult();
            await sized.Task;
        }

        if (element is ReactorHostControl host)
        {
            await host.WaitForIdleAsync();
            host.UpdateLayout();
        }
    }

    public static string ReadLog(Shell shell)
    {
        using var stream = SharedFile.OpenToRead(shell.Paths.Logs);
        using var reader = new StreamReader(stream);
        return reader.ReadToEnd();
    }

    internal static List<string> LeftOpenBy(TestContext test)
    {
        return Left.TryRemove(test, out var names) ? names : [];
    }

    private static string Describe(HostWindow window)
    {
        var content = window.Client.Content;
        var name = window.Client is FloatingWindow floating ? floating.Title :
            content is null ? "" : AutomationProperties.GetName(content);
        return $"{window.Client.GetType().Name} \"{name}\" showing {content?.GetType().Name ?? "nothing"}";
    }
}

[TestClass]
public static class NoWindowLeftOpen
{
    [GlobalTestCleanup]
    public static void After(TestContext context)
    {
        var left = ShellFixture.LeftOpenBy(context);
        Assert.IsTrue(left.Count == 0, $"The test left {left.Count} shell window(s) open: {string.Join("; ", left)}.");
    }
}

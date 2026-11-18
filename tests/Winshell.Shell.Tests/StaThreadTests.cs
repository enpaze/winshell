using System.Runtime.CompilerServices;

namespace Winshell.ShellTests;

public sealed class StaThreadTests
{
    [Fact]
    public void From_an_MTA_thread_the_body_runs_to_its_end_on_another_thread_in_an_STA()
    {
        var (caller, ran) = On(ApartmentState.MTA, Run);

        Assert.Equal(ApartmentState.STA, ran.Apartment);
        Assert.NotEqual(caller, ran.Thread);
        Assert.True(ran.Finished);
    }

    [Fact]
    public void From_an_STA_thread_the_body_runs_on_the_calling_thread()
    {
        var (caller, ran) = On(ApartmentState.STA, Run);

        Assert.Equal(ApartmentState.STA, ran.Apartment);
        Assert.Equal(caller, ran.Thread);
        Assert.True(ran.Finished);
    }

    [Fact]
    public void From_an_MTA_thread_a_failure_is_rethrown_with_its_original_stack()
    {
        var thrown = new InvalidOperationException("The shell failed.");

        var caught = On(ApartmentState.MTA, () => Record.Exception(() => StaThread.Run(() => Fail(thrown))));

        Assert.Same(thrown, caught);
        Assert.Contains(nameof(Fail), thrown.StackTrace, StringComparison.Ordinal);
    }

    private static (int Caller, Ran Ran) Run()
    {
        var ran = new Ran();
        StaThread.Run(() =>
        {
            ran.Thread = Environment.CurrentManagedThreadId;
            ran.Apartment = Thread.CurrentThread.GetApartmentState();
            Thread.Sleep(50);
            ran.Finished = true;
        });
        return (Environment.CurrentManagedThreadId, ran);
    }

    [MethodImpl(MethodImplOptions.NoInlining)]
    private static void Fail(Exception exception)
    {
        throw exception;
    }

    private static T On<T>(ApartmentState apartment, Func<T> body)
    {
        T result = default!;
        var thread = new Thread(() => result = body());
        thread.SetApartmentState(apartment);
        thread.Start();
        thread.Join();
        return result;
    }

    private sealed class Ran
    {
        public int Thread { get; set; }

        public ApartmentState Apartment { get; set; }

        public bool Finished { get; set; }
    }
}

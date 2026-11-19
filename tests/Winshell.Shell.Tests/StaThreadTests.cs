using System.Globalization;
using System.Runtime.CompilerServices;

namespace Winshell.ShellTests;

[TestClass]
public sealed class StaThreadTests
{
    [TestMethod]
    public void From_an_MTA_thread_the_body_runs_to_its_end_on_another_thread_in_an_STA()
    {
        var (caller, ran) = On(ApartmentState.MTA, Run);

        Assert.AreEqual(ApartmentState.STA, ran.Apartment);
        Assert.AreNotEqual(caller, ran.Thread);
        Assert.IsTrue(ran.Finished);
    }

    [TestMethod]
    public void From_an_STA_thread_the_body_runs_on_the_calling_thread()
    {
        var (caller, ran) = On(ApartmentState.STA, Run);

        Assert.AreEqual(ApartmentState.STA, ran.Apartment);
        Assert.AreEqual(caller, ran.Thread);
        Assert.IsTrue(ran.Finished);
    }

    [TestMethod]
    public void From_an_MTA_thread_a_failure_is_rethrown_with_its_original_stack()
    {
        var thrown = new InvalidOperationException("The shell failed.");

        var caught = On(ApartmentState.MTA, () =>
        {
            try
            {
                StaThread.Run(() => Fail(thrown));
                return null;
            }
            catch (Exception exception)
            {
                return exception;
            }
        });

        Assert.AreSame(thrown, caught);
        Assert.Contains(nameof(Fail), thrown.StackTrace!, StringComparison.Ordinal);
    }

    [TestMethod]
    public void From_an_MTA_thread_the_body_keeps_the_callers_culture_and_UI_culture()
    {
        var culture = CultureInfo.GetCultureInfo("fr-FR");
        var uiCulture = CultureInfo.GetCultureInfo("ja-JP");

        var ran = On(ApartmentState.MTA, () =>
        {
            CultureInfo.CurrentCulture = culture;
            CultureInfo.CurrentUICulture = uiCulture;
            (CultureInfo Culture, CultureInfo UICulture, int Thread) seen = default;
            StaThread.Run(() => seen = (CultureInfo.CurrentCulture, CultureInfo.CurrentUICulture,
                Environment.CurrentManagedThreadId));
            return (Caller: Environment.CurrentManagedThreadId, Seen: seen);
        });

        Assert.AreNotEqual(ran.Caller, ran.Seen.Thread);
        Assert.AreEqual(culture, ran.Seen.Culture);
        Assert.AreEqual(uiCulture, ran.Seen.UICulture);
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

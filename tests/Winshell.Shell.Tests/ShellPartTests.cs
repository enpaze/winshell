using System.Reflection;

namespace Winshell.ShellTests;

public sealed class ShellPartTests
{
    [Fact]
    public async Task Adding_a_part_starts_it_once_and_returns_it_however_often_it_is_added()
    {
        var (returned, starts) = await ShellFixture.On(shell =>
        {
            using var part = new Part(shell);
            var first = shell.Add(part);
            var second = shell.Add(part);
            return (ReferenceEquals(first, part) && ReferenceEquals(second, part), part.Starts);
        });

        Assert.True(returned);
        Assert.Equal(1, starts);
    }

    [Fact]
    public async Task A_part_that_disposes_itself_is_no_longer_held_so_adding_it_again_starts_it_again()
    {
        var starts = await ShellFixture.On(shell =>
        {
            var part = new Part(shell);
            shell.Add(part);
            part.Dispose();
            shell.Add(part);
            part.Dispose();
            return part.Starts;
        });

        Assert.Equal(2, starts);
    }

    [Fact]
    public async Task A_part_whose_start_throws_is_not_held()
    {
        var starts = await ShellFixture.On(shell =>
        {
            var part = new Part(shell) { Fails = true };
            Assert.Throws<InvalidOperationException>(() => shell.Add(part));
            part.Fails = false;
            shell.Add(part);
            part.Dispose();
            return part.Starts;
        });

        Assert.Equal(2, starts);
    }

    [Fact]
    public async Task Removing_a_part_lets_go_of_it_without_disposing_it()
    {
        var (starts, disposed) = await ShellFixture.On(shell =>
        {
            var part = new Part(shell);
            shell.Add(part);
            shell.Remove(part);
            var disposedThen = part.Disposed;
            shell.Add(part);
            part.Dispose();
            return (part.Starts, disposedThen);
        });

        Assert.Equal(2, starts);
        Assert.False(disposed);
    }

    [Fact]
    public void Only_an_object_can_be_added_so_the_shell_holds_and_disposes_the_part_itself_not_a_copy()
    {
        var part = typeof(Shell).GetMethod(nameof(Shell.Add))!.GetGenericArguments().Single();

        Assert.True(part.GenericParameterAttributes.HasFlag(GenericParameterAttributes.ReferenceTypeConstraint));
        Assert.Equal([typeof(IDisposable)], part.GetGenericParameterConstraints());
    }

    private sealed class Part(Shell shell) : IShellPart
    {
        public int Starts { get; private set; }

        public bool Fails { get; set; }

        public bool Disposed { get; private set; }

        public void Start()
        {
            Starts++;
            if (Fails)
                throw new InvalidOperationException("The part could not start.");
        }

        public void Dispose()
        {
            Disposed = true;
            shell.Remove(this);
        }
    }
}

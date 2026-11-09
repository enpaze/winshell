using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;

namespace Winshell.ShellTests;

public sealed class WatchTests
{
    [Fact]
    public async Task A_watch_runs_again_once_for_each_turn_that_changed_what_it_read()
    {
        await ShellFixture.On(async shell =>
        {
            var source = new Source();
            var items = new ObservableList<string>();
            var text = new TextBlock();
            var runs = 0;
            text.Watch(() =>
            {
                runs++;
                text.Text = $"{source.First} {source.Second} {items.Count}";
            });
            using var window = new PanelWindow { Width = 120, Height = 40, Content = text, Visible = true };
            await Loaded(text);
            var before = runs;

            source.Unread = 1;
            await NextTurn(shell);
            Assert.Equal(before, runs);

            source.First = 1;
            source.Second = 1;
            await NextTurn(shell);
            Assert.Equal(before + 1, runs);
            Assert.Equal("1 1 0", text.Text);

            items.Add("one");
            await NextTurn(shell);
            Assert.Equal(before + 2, runs);
            Assert.Equal("1 1 1", text.Text);
            return true;
        });
    }

    [Fact]
    public async Task A_change_the_block_makes_itself_does_not_run_it_again()
    {
        await ShellFixture.On(async shell =>
        {
            var source = new Source();
            var text = new TextBlock();
            var runs = 0;
            text.Watch(() =>
            {
                runs++;
                text.Text = $"{source.Second}";
                source.Second = source.First * 2;
            });
            using var window = new PanelWindow { Width = 120, Height = 40, Content = text, Visible = true };
            await Loaded(text);
            var before = runs;

            source.First = 1;
            await NextTurn(shell);
            await NextTurn(shell);
            Assert.Equal(before + 1, runs);
            Assert.Equal(2, source.Second);
            return true;
        });
    }

    [Fact]
    public async Task A_watch_pauses_while_its_control_is_unloaded_and_stops_with_its_window()
    {
        await ShellFixture.On(async shell =>
        {
            var source = new Source();
            var text = new TextBlock();
            var runs = 0;
            text.Watch(() =>
            {
                runs++;
                text.Text = $"{source.First}";
            });
            using var window = new PanelWindow { Width = 120, Height = 40, Content = text, Visible = true };
            await Loaded(text);
            var unloaded = new TaskCompletionSource();
            text.Unloaded += (_, _) => unloaded.TrySetResult();
            window.Content = new Grid();
            await unloaded.Task.WaitAsync(TestContext.Current.CancellationToken);
            var before = runs;

            source.First = 1;
            await NextTurn(shell);
            Assert.Equal(before, runs);

            window.Content = text;
            await Loaded(text);
            Assert.Equal(before + 1, runs);
            Assert.Equal("1", text.Text);

            window.Dispose();
            source.First = 2;
            await NextTurn(shell);
            Assert.Equal(before + 1, runs);

            // The watch ended with its window, so loading the control into another does not start it again.
            using var next = new PanelWindow { Width = 120, Height = 40, Content = text, Visible = true };
            await Loaded(text);
            Assert.Equal(before + 1, runs);
            return true;
        });
    }

    [Fact]
    public async Task A_watch_follows_its_control_to_another_parent()
    {
        await ShellFixture.On(async shell =>
        {
            var source = new Source();
            var text = new TextBlock();
            var runs = 0;
            text.Watch(() =>
            {
                runs++;
                text.Text = $"{source.First}";
            });
            var from = new StackPanel { Children = { text } };
            var to = new StackPanel();
            using var window = new PanelWindow
            {
                Width = 120,
                Height = 40,
                Content = new Grid { Children = { from, to } },
                Visible = true
            };
            await Loaded(text);
            var unloaded = new TaskCompletionSource();
            text.Unloaded += (_, _) => unloaded.TrySetResult();
            var loaded = new TaskCompletionSource();
            text.Loaded += (_, _) => loaded.TrySetResult();
            from.Children.Remove(text);
            to.Children.Add(text);
            await Task.WhenAll(unloaded.Task, loaded.Task).WaitAsync(TestContext.Current.CancellationToken);
            var before = runs;

            source.First = 1;
            await NextTurn(shell);
            Assert.Equal(before + 1, runs);
            Assert.Equal("1", text.Text);
            return true;
        });
    }

    [Fact]
    public async Task A_block_that_throws_is_logged_and_runs_again_on_the_next_change()
    {
        await ShellFixture.On(async shell =>
        {
            var source = new Source();
            var text = new TextBlock();
            var broken = $"A broken block {Guid.NewGuid()}";
            text.Watch(() =>
            {
                if (source.First == 1)
                    throw new InvalidOperationException(broken);
                text.Text = $"{source.First}";
            });
            using var window = new PanelWindow { Width = 120, Height = 40, Content = text, Visible = true };
            await Loaded(text);

            source.First = 1;
            await NextTurn(shell);
            Assert.Contains($"ERROR watch: System.InvalidOperationException: {broken}", ShellFixture.ReadLog(shell),
                StringComparison.Ordinal);

            source.First = 2;
            await NextTurn(shell);
            Assert.Equal("2", text.Text);
            return true;
        });
    }

    /// <summary>Completes once the control has loaded, after its watch has run for it.</summary>
    private static Task Loaded(FrameworkElement control)
    {
        if (control.IsLoaded)
            return Task.CompletedTask;
        var loaded = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        control.Loaded += (_, _) => loaded.TrySetResult();
        return loaded.Task.WaitAsync(TestContext.Current.CancellationToken);
    }

    /// <summary>Completes after the work the shell's thread has queued so far, which holds a watch's next run.</summary>
    private static Task NextTurn(Shell shell)
    {
        var turn = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        shell.Loop.Post(turn.SetResult);
        return turn.Task.WaitAsync(TestContext.Current.CancellationToken);
    }

    private sealed class Source : ObservableObject
    {
        public int First
        {
            get => Read(field);
            set => Set(ref field, value);
        }

        public int Second
        {
            get => Read(field);
            set => Set(ref field, value);
        }

        public int Unread
        {
            get => Read(field);
            set => Set(ref field, value);
        }
    }
}

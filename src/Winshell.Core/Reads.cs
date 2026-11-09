using System.Collections.Specialized;
using System.ComponentModel;
using System.Runtime.CompilerServices;

namespace Winshell;

/// <summary>
/// The service properties and lists a block read while <see cref="Record"/> ran it. A property reports its read with
/// <see cref="Property"/> and a list with <see cref="List"/>. While nothing records on the thread, a report costs one
/// check. <c>Watch</c> in Winshell.Shell records its block and follows what it read.
/// </summary>
internal sealed class Reads
{
    [ThreadStatic]
    private static Reads? _recording;

    /// <summary>What a block that read nothing read.</summary>
    public static readonly Reads None = new();

    /// <summary>Each object read, with the names of the properties read on it.</summary>
    public Dictionary<INotifyPropertyChanged, HashSet<string>> Properties { get; } =
        new(ReferenceEqualityComparer.Instance);

    /// <summary>The lists that were enumerated, counted or indexed.</summary>
    public HashSet<INotifyCollectionChanged> Lists { get; } = new(ReferenceEqualityComparer.Instance);

    /// <summary>
    /// Runs <paramref name="block"/> on this thread and keeps what it read. A block that throws keeps the reads it made
    /// before the throw, and the exception goes on to the caller. A block recorded inside another keeps its reads to
    /// itself.
    /// </summary>
    public void Record(Action block)
    {
        var outer = _recording;
        _recording = this;
        try
        {
            block();
        }
        finally
        {
            _recording = outer;
        }
    }

    /// <summary>Reports a read of <paramref name="property"/> on <paramref name="source"/>, and returns <paramref name="value"/>.</summary>
    public static T Property<T>(INotifyPropertyChanged source, T value, [CallerMemberName] string property = "")
    {
        if (_recording is { } reads)
        {
            if (!reads.Properties.TryGetValue(source, out var names))
                reads.Properties[source] = names = [];
            names.Add(property);
        }

        return value;
    }

    /// <summary>Reports a read of <paramref name="list"/>'s items.</summary>
    public static void List(INotifyCollectionChanged list)
    {
        _recording?.Lists.Add(list);
    }
}

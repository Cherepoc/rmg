namespace Rmg.Core.Events;

/// <summary>A layer's part in a state's value, such as the song's +1 to a drum's rhythm speed.</summary>
public sealed record StateContribution(string Layer, object Value)
{
    /// <summary>The layer given to state that no layer was named for, such as the state that changes by bar.</summary>
    public const string UnlabeledLayer = "(unlabeled)";

    internal static StateContribution Unlabeled(object value)
    {
        return new StateContribution(UnlabeledLayer, value);
    }
}

/// <summary>A point of the song's generation that a trace records, with the state it had there.</summary>
/// <param name="Point">What was decided there, such as a track's rhythm for a bar or the chord shape of a note.</param>
/// <param name="Bar">The bar of the section's 4-bar pattern.</param>
/// <param name="Position">Where in the bar, in beats; 0 for what holds for the whole bar.</param>
public sealed record StateTraceEntry(string Point, int Track, int Section, int Bar, double Position, StateMap StateMap);

/// <summary>
///     Records what every layer contributed to the state of the song being generated on this thread, to answer why a
///     value came out as it did. It costs nothing when no trace runs: states then keep no record of their layers.
/// </summary>
/// <example>
///     <code>
///     using var trace = StateTrace.Start();
///     SongGenerator.GenerateSong(seed);
///     foreach (var entry in trace.Entries)
///         Console.WriteLine(string.Join(", ", entry.StateMap.Explain(kind)));
///     </code>
/// </example>
public sealed class StateTrace : IDisposable
{
    [ThreadStatic] private static StateTrace? _current;

    // how many traces run on any thread, so that a thread with none checks a plain field and not its own
    private static int _runningCount;

    private readonly List<StateTraceEntry> _entries = [];

    private StateTrace()
    {
    }

    public IReadOnlyList<StateTraceEntry> Entries => _entries;

    internal static bool IsRunning => Volatile.Read(ref _runningCount) > 0 && _current is not null;

    public void Dispose()
    {
        if (!ReferenceEquals(_current, this))
            return;

        _current = null;
        Interlocked.Decrement(ref _runningCount);
    }

    /// <summary>Starts recording on this thread until the trace is disposed.</summary>
    public static StateTrace Start()
    {
        if (_current is not null)
            throw new InvalidOperationException("A state trace already runs on this thread.");

        var trace = new StateTrace();
        _current = trace;
        Interlocked.Increment(ref _runningCount);
        return trace;
    }

    internal static void Record(string point, int track, int section, int bar, StateMap stateMap, double position = 0)
    {
        if (IsRunning)
            _current!._entries.Add(new StateTraceEntry(point, track, section, bar, position, stateMap));
    }
}

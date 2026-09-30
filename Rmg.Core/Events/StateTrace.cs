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
/// <param name="Phrase">
///     The phrase scheme of the section's 4-bar pattern, such as AABA, where it is known, or what was decided, in words.
/// </param>
/// <param name="Value">What was decided, as a value to read, such as a fill's span; none where the state says it.</param>
public sealed record StateTraceEntry(
    string Point,
    int Track,
    int Section,
    int Bar,
    double Position,
    StateMap StateMap,
    string? Phrase = null,
    object? Value = null
);

/// <summary>
///     Records the points of the song being generated in this flow of execution (the thread, or the async method and
///     what it awaits), and, where it explains, what every layer contributed to their state, to answer why a value came
///     out as it did. It costs nothing when no trace runs: states then keep no record of their layers.
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
    // the trace of this flow of execution, which follows an async method from thread to thread
    private static readonly AsyncLocal<StateTrace?> Current = new();

    // how many traces run anywhere, so that where none does, a plain field is checked and not the flow's own
    private static int _runningCount;

    private bool _isDisposed;

    // whether states keep what every layer contributed, which costs most of what the trace does
    private readonly bool _explains;

    // how many pauses hold the trace here, which records nothing while one does
    private int _pauseCount;

    private readonly List<StateTraceEntry> _entries = [];

    private StateTrace(bool explains)
    {
        _explains = explains;
    }

    public IReadOnlyList<StateTraceEntry> Entries => _entries;

    internal static bool IsRunning => Volatile.Read(ref _runningCount) > 0 && Current.Value is { _isDisposed: false, _pauseCount: 0 };

    /// <summary>Whether a trace runs here that keeps what every layer contributed to a state (<see cref="StateMap.Explain" />).</summary>
    internal static bool IsExplaining => Volatile.Read(ref _runningCount) > 0 && Current.Value is { _isDisposed: false, _pauseCount: 0, _explains: true };

    /// <summary>
    ///     Stops recording in this flow of execution until the pause is disposed, for work done again that was recorded
    ///     the first time, such as a section's melody built afresh for a later appearance.
    /// </summary>
    internal static IDisposable Pause()
    {
        var trace = Current.Value is { _isDisposed: false } current ? current : null;
        if (trace is not null)
            trace._pauseCount++;
        return new Paused(trace);
    }

    private sealed class Paused(StateTrace? trace) : IDisposable
    {
        private StateTrace? _trace = trace;

        public void Dispose()
        {
            if (_trace is not null)
                _trace._pauseCount--;
            _trace = null;
        }
    }

    public void Dispose()
    {
        if (_isDisposed)
            return;

        _isDisposed = true;
        if (ReferenceEquals(Current.Value, this))
            Current.Value = null;
        Interlocked.Decrement(ref _runningCount);
    }

    /// <summary>Starts recording in this flow of execution until the trace is disposed.</summary>
    /// <param name="explains">
    ///     Whether states keep what every layer contributed to them, for <see cref="StateMap.Explain" />, which makes
    ///     generation several times slower; the entries and their values are recorded either way.
    /// </param>
    public static StateTrace Start(bool explains = true)
    {
        if (Current.Value is { _isDisposed: false })
            throw new InvalidOperationException("A state trace already runs here.");

        var trace = new StateTrace(explains);
        Current.Value = trace;
        Interlocked.Increment(ref _runningCount);
        return trace;
    }

    internal static void Record(
        string point,
        int track,
        int section,
        int bar,
        StateMap stateMap,
        double position = 0,
        string? phrase = null,
        object? value = null
    )
    {
        if (IsRunning)
            Current.Value!._entries.Add(new StateTraceEntry(point, track, section, bar, position, stateMap, phrase, value));
    }
}

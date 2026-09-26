using System.Collections.Immutable;

namespace Rmg.Core.Rendering;

/// <summary>
///     Places the chords of a track one after another, each in the layout that follows best from the chord before.
///     The layouts it chooses from are the chord's own inversions, its lowest notes moved up an octave in turn, in
///     every octave of the track's range; a chord whose layout is fixed is only moved by whole octaves. How a layout
///     follows depends on the smoothness: at 1 its notes move as little as they can from the chord before, and at 0 it
///     keeps that chord's shape, slid with the root. A little pull towards the middle of the range keeps the chords
///     from drifting to an edge. The first chord, and the first of a bar that starts afresh, play as drawn.
/// </summary>
internal sealed class VoiceLeader
{
    /// <summary>How much more a move of the top note counts than a move of another, since the ear follows it.</summary>
    internal const double TopNoteWeight = 1;

    /// <summary>How much every semitone of the layout's middle away from the range's middle counts.</summary>
    internal const double CentrePull = 0.25;

    private const int OctaveNoteCount = 12;

    private readonly Func<ImmutableArray<int>, ImmutableArray<int>> _placeAsDrawn;
    private readonly int _minNote;
    private readonly int _maxNote;

    private ImmutableArray<int> _previousDrawn;
    private ImmutableArray<int> _previous;
    private int _previousRoot;
    private int _previousReset;

    /// <param name="minNote">The lowest note of the track's range.</param>
    /// <param name="maxNote">The highest note of the track's range.</param>
    /// <param name="placeAsDrawn">How a chord is placed in the range when it is not led from the one before.</param>
    public VoiceLeader(int minNote, int maxNote, Func<ImmutableArray<int>, ImmutableArray<int>> placeAsDrawn)
    {
        ArgumentOutOfRangeException.ThrowIfGreaterThan(minNote, maxNote);

        _minNote = minNote;
        _maxNote = maxNote;
        _placeAsDrawn = placeAsDrawn;
    }

    /// <param name="drawn">The chord's notes as drawn, ascending.</param>
    /// <param name="root">The chord root's note, which tells how far the root moved.</param>
    /// <param name="smoothness">How smoothly the chord follows the one before, from 0 to 1.</param>
    /// <param name="reset">The number of a bar that starts afresh, 0 for none; its first chord plays as drawn.</param>
    public ImmutableArray<int> Place(ImmutableArray<int> drawn, int root, bool isVoicingFixed, double smoothness, int reset)
    {
        var isReset = reset != 0 && reset != _previousReset;
        _previousReset = reset;

        ImmutableArray<int> placed;
        if (_previous.IsDefault || isReset)
            placed = _placeAsDrawn(drawn);
        else if (drawn.SequenceEqual(_previousDrawn) && root == _previousRoot)
            // the same chord again keeps its layout
            placed = _previous;
        else
            placed = Choose(drawn, isVoicingFixed, _previous, root - _previousRoot, Math.Clamp(smoothness, 0, 1), _minNote, _maxNote)
                ?? _placeAsDrawn(drawn);

        _previousDrawn = drawn;
        _previous = placed;
        _previousRoot = root;
        return placed;
    }

    /// <summary>
    ///     The layout of the chord that follows best from the previous one, or none if no layout fits the range.
    /// </summary>
    /// <param name="rootMotion">How far the root moved, in semitones; taken to the nearest octave.</param>
    internal static ImmutableArray<int>? Choose(
        ImmutableArray<int> drawn,
        bool isVoicingFixed,
        ImmutableArray<int> previous,
        int rootMotion,
        double smoothness,
        int minNote,
        int maxNote
    )
    {
        // the previous chord's shape slid with the root, by the smaller way round
        var motion = (rootMotion % OctaveNoteCount + OctaveNoteCount + OctaveNoteCount / 2) % OctaveNoteCount - OctaveNoteCount / 2;
        ImmutableArray<int> slid = [..previous.Select(x => x + motion)];
        var centre = (minNote + maxNote) / 2.0;

        ImmutableArray<int>? best = null;
        var bestCost = double.MaxValue;
        foreach (var layout in GetLayouts(drawn, isVoicingFixed, minNote, maxNote))
        {
            var cost = smoothness * GetMovement(previous, layout)
                + (1 - smoothness) * GetMovement(slid, layout)
                + CentrePull * Math.Abs(layout.Average() - centre);
            if (cost < bestCost)
            {
                best = layout;
                bestCost = cost;
            }
        }

        return best;
    }

    /// <summary>
    ///     The layouts of the chord that fit the range: its inversions, the lowest notes moved up an octave in turn,
    ///     unless its layout is fixed, each in every octave the range has room for.
    /// </summary>
    internal static IEnumerable<ImmutableArray<int>> GetLayouts(ImmutableArray<int> drawn, bool isVoicingFixed, int minNote, int maxNote)
    {
        var notes = drawn.Order().ToArray();
        var inversionCount = isVoicingFixed ? 1 : notes.Length;
        for (var inversion = 0; inversion < inversionCount; inversion++)
        {
            ImmutableArray<int> inverted = [..notes.Select((x, i) => i < inversion ? x + OctaveNoteCount : x).Order()];
            var lowestShift = (int)Math.Ceiling((minNote - inverted[0]) / (double)OctaveNoteCount);
            var highestShift = (int)Math.Floor((maxNote - inverted[^1]) / (double)OctaveNoteCount);
            for (var shift = lowestShift; shift <= highestShift; shift++)
                yield return [..inverted.Select(x => x + shift * OctaveNoteCount)];
        }
    }

    /// <summary>
    ///     How far one chord's notes move to become another's: every note's distance to the nearest note of the other
    ///     chord, both ways, so that chords of different sizes compare, and the top note's move counted once more.
    /// </summary>
    internal static double GetMovement(ImmutableArray<int> from, ImmutableArray<int> to)
    {
        double movement = 0;
        foreach (var note in to)
            movement += from.Min(x => Math.Abs(x - note));
        foreach (var note in from)
            movement += to.Min(x => Math.Abs(x - note));
        return movement + TopNoteWeight * Math.Abs(to.Max() - from.Max());
    }
}

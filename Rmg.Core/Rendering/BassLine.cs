using Rmg.Core.Composition;

namespace Rmg.Core.Rendering;

/// <summary>The chord a note is played over: the pitch of any step of the scale, counted from the chord's root.</summary>
internal sealed class ChordContext(Func<int, int> getPitch)
{
    /// <summary>The pitch of the note the steps above the root, or below it for negative steps.</summary>
    public int GetPitch(int stepsAboveRoot)
    {
        return getPitch(stepsAboveRoot);
    }

    public int Root => GetPitch(0);
}

/// <summary>
///     Places the notes of a bass line one after another. Every note takes the octave nearest the note before, within
///     the track's range, so the line moves by small steps. Chords change at bar lines: the first note of a bar lands
///     on what the bar asks for, most often the root, and a note in the last beat before a bar line leads into the
///     root of the next note's chord as its bar asks, such as a semitone below it. The first note plays as drawn.
/// </summary>
internal sealed class BassLine
{
    private const int OctaveNoteCount = 12;

    // chords change at bar lines, every 4 beats
    private const double BarDuration = 4;

    private readonly Func<int, int> _placeAsDrawn;
    private readonly int _minNote;
    private readonly int _maxNote;

    private int? _previousNote;
    private int? _previousBar;

    /// <param name="placeAsDrawn">How a note is placed in the range when there is no note before it.</param>
    public BassLine(int minNote, int maxNote, Func<int, int> placeAsDrawn)
    {
        if (maxNote - minNote < OctaveNoteCount - 1)
            throw new ArgumentException("The range must hold an octave.", nameof(maxNote));

        _minNote = minNote;
        _maxNote = maxNote;
        _placeAsDrawn = placeAsDrawn;
    }

    /// <param name="drawnNote">The note the line would play, whose pitch class is kept when nothing else applies.</param>
    /// <param name="next">The chord of the next note, if there is one.</param>
    /// <param name="nextPosition">The position of the next note, in beats.</param>
    public int Place(
        int drawnNote,
        ChordContext chord,
        double position,
        ChordContext? next,
        double? nextPosition,
        ChordArrival arrival,
        ChordApproach approach
    )
    {
        // the bar, not the root, tells a new chord, since a bass line's own walk can move its root within one
        var bar = (int)Math.Floor(position / BarDuration);
        var isArrival = bar != _previousBar;

        int note;
        int? anchor = null;
        if (isArrival)
            note = arrival switch
            {
                ChordArrival.Root => chord.Root,
                ChordArrival.Third => chord.GetPitch(2),
                ChordArrival.Fifth => chord.GetPitch(4),
                _ => drawnNote
            };
        else if (approach != ChordApproach.None
            && next is not null
            && nextPosition is { } nextNotePosition
            && IsInLastBeatBeforeChange(position, nextNotePosition))
            (note, anchor) = GetApproach(approach, next);
        else
            note = drawnNote;

        // an approach sits next to the root it leads into, and every other note near the note before
        var placed = (anchor ?? _previousNote) is { } nearTo ? GetNearest(note, nearTo) : _placeAsDrawn(note);
        _previousNote = placed;
        _previousBar = bar;
        return placed;
    }

    /// <summary>Whether the note is in the last beat before the next bar line, and the next note is past it.</summary>
    internal static bool IsInLastBeatBeforeChange(double position, double nextPosition)
    {
        var change = (Math.Floor(position / BarDuration) + 1) * BarDuration;
        return position >= change - 1 && nextPosition >= change;
    }

    /// <summary>
    ///     The note that leads into the next chord's root, from the side the line comes from, and where that root will
    ///     be, which the note is placed next to.
    /// </summary>
    private (int Note, int Target) GetApproach(ChordApproach approach, ChordContext next)
    {
        var target = _previousNote is { } previous ? GetNearest(next.Root, previous) : next.Root;
        var isFromBelow = (_previousNote ?? target) <= target;
        var note = approach switch
        {
            ChordApproach.ScaleStep => isFromBelow ? next.GetPitch(-1) : next.GetPitch(1),
            ChordApproach.HalfStepBelow => target - 1,
            ChordApproach.HalfStepAbove => target + 1,
            ChordApproach.Fifth => next.GetPitch(4),
            ChordApproach.Anticipation => target,
            _ => throw new ArgumentOutOfRangeException(nameof(approach), approach, null)
        };
        return (note, target);
    }

    /// <summary>The note's pitch class in the octave nearest the other note, within the range; the lower on a tie.</summary>
    internal int GetNearest(int note, int other)
    {
        var pitchClass = note.Mod(OctaveNoteCount);
        var lowest = _minNote + (pitchClass - _minNote).Mod(OctaveNoteCount);
        var nearest = lowest;
        for (var candidate = lowest; candidate <= _maxNote; candidate += OctaveNoteCount)
            if (Math.Abs(candidate - other) < Math.Abs(nearest - other))
                nearest = candidate;
        return nearest;
    }
}

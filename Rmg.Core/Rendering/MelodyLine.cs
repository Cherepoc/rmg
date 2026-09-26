using Rmg.Core;

namespace Rmg.Core.Rendering;

/// <summary>
///     Places the notes of a melody one after another, each by rule from the note before, the chord and the scale.
///     A note means to go on the way the melody goes or to turn back, so the melody runs a while before it turns. A
///     note on a strong beat takes a note of the chord, the nearest one the way it goes, or the one after for a leap; a note on a weak beat moves along the scale, a step or, for a leap, a third, passing between the chord's
///     notes. After a leap the melody steps back the other way, as a melody fills the gap it left, and when it strays
///     too far from where its phrase aims, it turns back towards it. It keeps to a singable range in the middle of the
///     track's, and its first note is the chord's note nearest where the phrase aims.
///     A bar pattern that comes back is a motif: its bar starts by the rules, and its other notes take the shape it had
///     the first time, in scale steps from its first note, so that over another chord it sounds as a sequence of it. A
///     note of the shape on a strong beat that misses the chord moves to a note of the chord close by.
/// </summary>
internal sealed class MelodyLine
{
    /// <summary>How wide the melody's range is, in semitones: an octave and a fourth, a little more than a voice sings comfortably, so that a line
    ///     that runs on turns at the phrase's aim more often than at the range's edge.</summary>
    internal const int RangeWidth = 17;

    /// <summary>How far from where its phrase aims the melody goes before it turns back towards it.</summary>
    internal const int RegisterPull = 7;

    /// <summary>A move this big or bigger is a leap, which the next note fills in by stepping back.</summary>
    internal const int LeapSize = 7;

    // chords change at bar lines, every 4 beats
    private const double BarDuration = 4;

    /// <summary>The weakest beat, by its rank in the rhythm, that still takes a note of the chord.</summary>
    internal const int StrongestWeakRank = 1;

    /// <summary>How far a motif's note on a strong beat moves to a note of the chord, at most.</summary>
    internal const int MotifChordToneReach = 2;

    private const int OctaveNoteCount = 12;

    private readonly int _low;
    private readonly int _high;
    private readonly double _middle;

    private int? _previous;
    private int _previousMove;

    // the way the melody goes, up (1) or down (-1): its last move's, which the next note goes on in or turns from, across
    // bar lines too; a bar that comes back takes its shape from the motif, not from the way the melody went
    private int _heading = 1;
    private int? _previousBar;

    // every motif's shape, as the scale steps of its notes from its first, in the order they play
    private readonly Dictionary<int, List<int>> _motifs = [];
    private List<int>? _recording;
    private List<int>? _replaying;
    private int _barNoteIndex;
    private int _barFirstStep;

    /// <param name="minNote">The lowest note of the track's range.</param>
    /// <param name="maxNote">The highest note of the track's range.</param>
    public MelodyLine(int minNote, int maxNote)
    {
        ArgumentOutOfRangeException.ThrowIfGreaterThan(minNote, maxNote);

        // the singable range in the middle of the track's, or all of the track's if it is narrower
        var middle = (minNote + maxNote) / 2.0;
        _low = Math.Max(minNote, (int)Math.Round(middle - RangeWidth / 2.0));
        _high = Math.Min(maxNote, _low + RangeWidth);
        _middle = (_low + _high) / 2.0;
    }

    /// <param name="chordToneClasses">The pitch classes of the chord's notes.</param>
    /// <param name="beatRank">How strong the note's beat is, 0 the strongest.</param>
    /// <param name="step">
    ///     Where the note means to go, from the way the melody goes: 1 on, -1 back, 2 and -2 the same with a leap, 0
    ///     staying.
    /// </param>
    /// <param name="register">How far above or below the middle of the range the phrase aims here, in semitones.</param>
    /// <param name="motif">The bar pattern the note belongs to; 0 for none.</param>
    public int Place(
        ChordContext chord,
        IReadOnlyCollection<int> chordToneClasses,
        double position,
        int beatRank,
        int step,
        double register,
        int motif = 0
    )
    {
        var bar = (int)Math.Floor(position / BarDuration);
        if (bar != _previousBar)
        {
            _barNoteIndex = 0;
            // a motif heard before plays its shape again, and one heard for the first time is remembered
            _replaying = motif != 0 && _motifs.TryGetValue(motif, out var shape) ? shape : null;
            _recording = motif != 0 && _replaying is null ? _motifs[motif] = [] : null;
        }

        _previousBar = bar;
        var note = _replaying is { } replaying && _barNoteIndex > 0 && _barNoteIndex < replaying.Count
            ? PlaceFromMotif(chord, chordToneClasses, beatRank, replaying[_barNoteIndex])
            : PlaceByRule(chord, chordToneClasses, beatRank, step, register);

        var noteStep = GetScaleStep(chord, note);
        if (_barNoteIndex == 0)
            _barFirstStep = noteStep;
        _recording?.Add(noteStep - _barFirstStep);
        _barNoteIndex++;

        _previousMove = _previous is { } before ? note - before : 0;
        if (_previousMove != 0)
            _heading = Math.Sign(_previousMove);
        _previous = note;
        return note;
    }

    /// <summary>
    ///     A note of a motif heard before: the scale step it had from the bar's first note, and on a strong beat the
    ///     note of the chord close by if it misses the chord; by the rules if that leaves the range.
    /// </summary>
    private int PlaceFromMotif(ChordContext chord, IReadOnlyCollection<int> chordToneClasses, int beatRank, int stepsFromFirst)
    {
        var note = chord.GetPitch(_barFirstStep + stepsFromFirst);
        if (beatRank <= StrongestWeakRank && chordToneClasses.Count > 0 && !chordToneClasses.Contains(note.Mod(OctaveNoteCount)))
        {
            var nearest = GetNearest(GetChordTones(chordToneClasses), note);
            if (Math.Abs(nearest - note) <= MotifChordToneReach)
                note = nearest;
        }

        return note >= _low && note <= _high ? note : GetNearest(GetScaleNotes(chord), note);
    }

    /// <summary>The scale step of a note, counted from the chord's root: the step whose note is nearest it.</summary>
    private static int GetScaleStep(ChordContext chord, int note)
    {
        // a scale step is between one and a few semitones, so the step is near the note's distance in sevenths of
        // an octave
        var guess = (int)Math.Round((note - chord.Root) * 7.0 / OctaveNoteCount);
        return Enumerable.Range(guess - 4, 9).MinBy(x => Math.Abs(chord.GetPitch(x) - note));
    }

    private int PlaceByRule(ChordContext chord, IReadOnlyCollection<int> chordToneClasses, int beatRank, int step, double register)
    {
        var aim = _middle + register;
        var isStrong = beatRank <= StrongestWeakRank && chordToneClasses.Count > 0;

        int note;
        if (_previous is not { } previous)
        {
            note = isStrong ? GetNearest(GetChordTones(chordToneClasses), aim) : GetNearest(GetScaleNotes(chord), aim);
        }
        else
        {
            var direction = Math.Sign(step) * _heading;
            var isLeap = Math.Abs(step) >= 2;
            if (Math.Abs(_previousMove) >= LeapSize)
            {
                // a leap is followed by a step back
                direction = -Math.Sign(_previousMove);
                isLeap = false;
            }
            else if (Math.Abs(previous - aim) > RegisterPull)
            {
                direction = Math.Sign(aim - previous);
            }

            var candidates = isStrong ? GetChordTones(chordToneClasses) : GetScaleNotes(chord);
            note = GetNext(candidates, previous, direction, isLeap ? 2 : 1);
        }

        return note;
    }

    /// <summary>
    ///     The candidate the given number of places from the previous note the way it goes, or the nearest one to it if
    ///     it stays; the other way if there are not enough that way, since the range ends there.
    /// </summary>
    internal static int GetNext(IReadOnlyList<int> candidates, int previous, int direction, int places)
    {
        if (direction == 0)
            return GetNearest(candidates, previous);

        var ahead = candidates.Where(x => Math.Sign(x - previous) == direction).OrderBy(x => Math.Abs(x - previous)).ToArray();
        if (ahead.Length > 0)
            return ahead[Math.Min(places, ahead.Length) - 1];

        var behind = candidates.Where(x => Math.Sign(x - previous) == -direction).OrderBy(x => Math.Abs(x - previous)).ToArray();
        return behind.Length > 0 ? behind[Math.Min(places, behind.Length) - 1] : GetNearest(candidates, previous);
    }

    private static int GetNearest(IReadOnlyList<int> candidates, double target)
    {
        return candidates.MinBy(x => Math.Abs(x - target));
    }

    /// <summary>The notes of the chord in the range.</summary>
    private IReadOnlyList<int> GetChordTones(IReadOnlyCollection<int> chordToneClasses)
    {
        return Enumerable.Range(_low, _high - _low + 1).Where(x => chordToneClasses.Contains(x % OctaveNoteCount)).ToArray();
    }

    /// <summary>The notes of the scale in the range, counted from the chord's root.</summary>
    private IReadOnlyList<int> GetScaleNotes(ChordContext chord)
    {
        // a scale step is at least a semitone, so this many steps either way of the root reach past the range
        var reach = (_high - _low) + Math.Abs(chord.Root - _low) + OctaveNoteCount;
        return Enumerable.Range(-reach, 2 * reach + 1)
            .Select(chord.GetPitch)
            .Where(x => x >= _low && x <= _high)
            .Distinct()
            .Order()
            .ToArray();
    }
}

using Rmg.Core;

namespace Rmg.Core.Composition;

/// <summary>
///     Places the notes of a melody one after another, each by rule from the note before, the chord and the scale.
///     A note means to go on the way the melody goes or to turn back, so the melody runs a while before it turns. A
///     note on a strong beat takes a note of the chord, the nearest one the way it goes, or the one after for a leap; a note on a weak beat moves along the scale, a step or, for a leap, a third, passing between the chord's
///     notes. After a leap the melody steps back the other way, as a melody fills the gap it left; it leans towards
///     where its phrase aims, going on towards it and turning back from it the likelier the further it is, and when it
///     strays too far, it turns back towards it. It keeps to a singable range in the middle of the
///     track's, and its first note is the chord's note nearest where the phrase aims.
///     A note that echoes one heard before, as the notes of a bar pattern that comes back or of a cycle that repeats the
///     one before do, plays it again: the scale step it had from its chord's root, from the root of its own, where a run
///     of echoes starts or its chord changes as it was heard over the same root, and in the octave nearest the note
///     before over another, and in the run's octave otherwise, so that over the same chords it repeats and over others it
///     sounds as a sequence. An echo on a strong
///     beat that misses the chord moves to the chord's note nearest it.
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


    /// <summary>The weakest beat, by its rank in the rhythm, that still takes a note of the chord.</summary>
    internal const int StrongestWeakRank = 1;

    private const int OctaveNoteCount = 12;

    private const int ScaleStepCount = 7;

    private readonly int _low;
    private readonly int _high;
    private readonly double _middle;

    private int? _previous;
    private int _previousMove;

    // the way the melody goes, up (1) or down (-1): its last move's, which the next note goes on in or turns from, across
    // bar lines too; an echo takes its step from the note it echoes, not from the way the melody went
    private int _heading = 1;

    // every note that may be echoed, as the scale step it had from its chord's root, by its key
    private readonly Dictionary<int, (int Step, int Root)> _heard = [];

    // the run of echoes playing: the root of its chord, and the octave it plays in, in scale steps from where it was heard
    private (int Root, int Octave)? _echoRun;

    /// <param name="minNote">The lowest note of the track's range.</param>
    /// <param name="maxNote">The highest note of the track's range.</param>
    public MelodyLine(int minNote, int maxNote)
    {
        ArgumentOutOfRangeException.ThrowIfGreaterThan(minNote, maxNote);

        (_low, _high) = GetSingableRange(minNote, maxNote);
        _middle = (_low + _high) / 2.0;
    }

    /// <param name="chordToneClasses">The pitch classes of the chord's notes.</param>
    /// <param name="beatRank">How strong the note's beat is, 0 the strongest.</param>
    /// <param name="step">How far the note means to go: 1 a step, 2 a leap, 0 staying.</param>
    /// <param name="turn">
    ///     The note's draw of whether it goes on the way the melody goes or turns back, from 0 to 1, which goes on
    ///     below the chance the aim leans (<see cref="MelodyLayers.GetContinueChance" />).
    /// </param>
    /// <param name="register">How far above or below the middle of the range the phrase aims here, in semitones.</param>
    /// <param name="echo">The key of the note it plays again, if that was heard, or is remembered by; 0 for none.</param>
    public int Place(
        ChordContext chord,
        IReadOnlyCollection<int> chordToneClasses,
        int beatRank,
        int step,
        double turn,
        double register,
        int echo = 0
    )
    {
        int note;
        if (echo != 0 && _heard.TryGetValue(echo, out var heard))
        {
            // over the root it was heard over, as it was; over another, a sequence nearest the note before
            if (_echoRun?.Root != chord.Root)
                _echoRun = (chord.Root, chord.Root == heard.Root ? 0 : GetNearestOctave(chord, heard.Step) - heard.Step);
            note = PlaceEcho(chord, chordToneClasses, beatRank, heard.Step + _echoRun.Value.Octave);
        }
        else
        {
            _echoRun = null;
            note = PlaceByRule(chord, chordToneClasses, beatRank, step, turn, register);
        }

        // the first time a note is heard it is remembered, as the step from its chord's root it has
        if (echo != 0)
            _heard.TryAdd(echo, (LinePlacement.GetScaleStep(chord, note), chord.Root));

        _previousMove = _previous is { } before ? note - before : 0;
        if (_previousMove != 0)
            _heading = Math.Sign(_previousMove);
        _previous = note;
        return note;
    }

    /// <summary>The scale step, an octave's steps from the one given, whose note is nearest the note before.</summary>
    private int GetNearestOctave(ChordContext chord, int step)
    {
        var target = _previous ?? _middle;
        return Enumerable.Range(-3, 7).Select(x => step + x * ScaleStepCount).MinBy(x => Math.Abs(chord.GetPitch(x) - target));
    }

    /// <summary>
    ///     An echo: the note of the scale step, from the chord's root, and on a strong beat, which takes a note of the
    ///     chord as it would by the rules, the note of the chord nearest it if it misses the chord; the nearest in the
    ///     range if it leaves it.
    /// </summary>
    private int PlaceEcho(ChordContext chord, IReadOnlyCollection<int> chordToneClasses, int beatRank, int step)
    {
        var note = chord.GetPitch(step);
        if (beatRank <= StrongestWeakRank && chordToneClasses.Count > 0 && !chordToneClasses.Contains(note.Mod(OctaveNoteCount)))
            note = GetNearest(GetChordTones(chordToneClasses), note);

        return note >= _low && note <= _high ? note : GetNearest(GetScaleNotes(chord), note);
    }

    /// <summary>The singable range in the middle of a track's, or all of the track's if it is narrower.</summary>
    internal static (int Low, int High) GetSingableRange(int minNote, int maxNote)
    {
        var middle = (minNote + maxNote) / 2.0;
        var low = Math.Max(minNote, (int)Math.Round(middle - RangeWidth / 2.0));
        return (low, Math.Min(maxNote, low + RangeWidth));
    }

    /// <summary>
    ///     The note that leads into the next by a step, where a chord changes: the note as it is, if it is a step (one or
    ///     two semitones) from the next already; or the note nearest it that is, a step from it at most, so that it bends
    ///     rather than jumps, a note of its scale on a weak beat and of its chord on a strong one, where it would sound
    ///     against its own chord; or the note as it is, where no note is.
    /// </summary>
    /// <param name="chordToneClasses">The pitch classes of the note's own chord's notes.</param>
    /// <param name="beatRank">How strong the note's beat is, 0 the strongest.</param>
    public int Approach(ChordContext chord, IReadOnlyCollection<int> chordToneClasses, int beatRank, int note, int next)
    {
        static bool IsStep(int a, int b) => Math.Abs(a - b) is 1 or 2;
        if (IsStep(note, next))
            return note;

        var isStrong = beatRank <= StrongestWeakRank && chordToneClasses.Count > 0;
        var candidates = (isStrong ? GetChordTones(chordToneClasses) : GetScaleNotes(chord))
            .Where(x => IsStep(x, next) && Math.Abs(x - note) <= 4)
            .ToArray();
        return candidates.Length == 0 ? note : GetNearest(candidates, note);
    }

    private int PlaceByRule(ChordContext chord, IReadOnlyCollection<int> chordToneClasses, int beatRank, int step, double turn, double register)
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
            // on the way the melody goes, or back, leaning towards the aim
            var goesOn = turn < MelodyLayers.GetContinueChance((aim - previous) * _heading);
            var direction = step == 0 ? 0 : goesOn ? _heading : -_heading;
            var isLeap = step >= 2;
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

using Rmg.Core;

namespace Rmg.Core.Composition;

/// <summary>
///     Places the notes of a line, such as the melody, one after another, each by rule from the note before, the chord
///     and the scale, as far as its profile has it (<see cref="LineProfile" />).
///     A note means to go on the way the melody goes or to turn back, so the melody runs a while before it turns. A
///     note on a strong beat takes a note of the chord, the nearest one the way it goes, or the one after for a leap; a note on a weak beat moves along the scale, a step or, for a leap, a third, passing between the chord's
///     notes. After a leap the melody steps back the other way, as a melody fills the gap it left; it leans towards
///     where its phrase aims, going on towards it and turning back from it the likelier the further it is, and when it
///     strays too far, it turns back towards it. It keeps to a singable range in the middle of the
///     track's, and its first note is the chord's note nearest where the phrase aims.
///     A note that echoes one heard before, as the notes of a bar pattern that comes back or of a cycle that repeats the
///     one before do, plays it again: the scale step it had from its chord's root, moved by as many steps as its phrase
///     now aims away from where it aimed then, so that the phrase's shape carries its figures, from the root of its own, where a run
///     of echoes starts or its chord changes as it was heard over the same root, and in the octave nearest the note
///     before over another, and in the run's octave otherwise, so that over the same chords it repeats and over others it
///     sounds as a sequence. An echo on a strong
///     beat that misses the chord moves to the chord's note nearest it.
/// </summary>
internal sealed class Line
{
    private readonly LineProfile _profile;

    private const int OctaveNoteCount = 12;

    // the range the line sings in, moved up or down its instrument's by its section's lift, and its middle; and where it
    // sits unmoved, and the instrument's, which bounds the move
    private int _low;
    private int _high;
    private double _middle;
    private readonly (int Low, int High) _unmoved;
    private readonly (int Low, int High) _instrument;

    private int? _previous;

    // the note the line last landed on a chord with, which a step down steps from
    private int? _landed;
    private int _previousMove;

    // the way the melody goes, up (1) or down (-1): its last move's, which the next note goes on in or turns from, across
    // bar lines too; an echo takes its step from the note it echoes, not from the way the melody went
    private int _heading = 1;

    // every note that may be echoed, as the scale step it had from its chord's root, by its key, with where its phrase
    // aimed then
    private readonly Dictionary<long, (int Step, int Root, double Aim)> _heard = [];

    // the run of echoes playing: the root of its chord and the octave it plays in, in scale steps from where it was heard
    private (int Root, int Octave)? _echoRun;

    // the pitch classes the line takes over the chord it plays over now, by its line scale; none for all of the section's scale
    private IReadOnlySet<int>? _classes;

    // whether the phrase playing goes on from the note before, and so replays its echoes nearest it rather than where
    // they were heard
    private bool _echoesNear;

    /// <param name="profile">How the line moves, and what its strong beats take.</param>
    /// <param name="minNote">The lowest note of the track's range.</param>
    /// <param name="maxNote">The highest note of the track's range.</param>
    public Line(LineProfile profile, int minNote, int maxNote)
    {
        ArgumentOutOfRangeException.ThrowIfGreaterThan(minNote, maxNote);

        _profile = profile;
        _unmoved = (_low, _high) = GetSingableRange(minNote, maxNote, profile.RangeWidth);
        _instrument = (minNote, maxNote);
        _middle = (_low + _high) / 2.0;
    }

    /// <summary>The line's range moved by a section's lift, as far as its share of it and the instrument's range let it.</summary>
    private void Lift(double lift)
    {
        var by = Math.Clamp((int)Math.Round(lift * _profile.ContourShare), _instrument.Low - _unmoved.Low, _instrument.High - _unmoved.High);
        (_low, _high) = (_unmoved.Low + by, _unmoved.High + by);
        _middle = (_low + _high) / 2.0;
    }

    /// <param name="chordToneClasses">The pitch classes of the chord's notes.</param>
    /// <param name="beatRank">How strong the note's beat is, 0 the strongest.</param>
    /// <param name="step">How far the note means to go: 1 a step, 2 a leap, 0 staying.</param>
    /// <param name="turn">
    ///     The note's draw of whether it goes on the way the melody goes or turns back, from 0 to 1, which goes on
    ///     below the chance the aim leans (<see cref="LineProfile.GetContinueChance" />).
    /// </param>
    /// <param name="register">How far above or below the middle of the range the phrase aims here, in semitones.</param>
    /// <param name="echo">The key of the note it plays again, if that was heard, or is remembered by; 0 for none.</param>
    /// <param name="landing">
    ///     What the note lands on, as the first note of a bar that asks for it, such as a bass's on the chord's root: the
    ///     nearest such note of the chord in the range to the note before; free for none, the note placed as it would be.
    /// </param>
    /// <param name="phraseStart">
    ///     How the note starts its phrase: afresh, as a first note does, at where the phrase aims, its echoes replayed where
    ///     they were heard; going on from the note before, its echoes replayed nearest it; none within a phrase.
    /// </param>
    /// <param name="lift">How far the section moves the line's range and its aim up its instrument's, in semitones (<see cref="CompositionStateKinds.LineLift" />).</param>
    /// <param name="drones">Whether the line drones, its weak notes on an open string, the key's own at the bottom of its range, its melody an octave or more above it.</param>
    public int Place(
        ChordContext chord,
        IReadOnlyCollection<int> chordToneClasses,
        int beatRank,
        int step,
        double turn,
        double register,
        long echo = 0,
        ChordArrival landing = ChordArrival.Free,
        PhraseStart phraseStart = PhraseStart.None,
        LineScale lineScale = LineScale.Section,
        double lift = 0,
        double pedal = 1,
        bool drones = false
    )
    {
        Lift(lift);
        _classes = LineScales.Classes(lineScale, chord);
        // a droning line's weak note on its open string, which leaves its melody where it was, so that the melody steps on
        // from its own last note over the drone
        if (drones && beatRank > 0)
            return OpenString(chord);
        var aim = register * _profile.ContourShare;
        if (phraseStart == PhraseStart.Afresh)
            (_previous, _previousMove, _echoRun) = (null, 0, null);
        if (phraseStart != PhraseStart.None)
            (_echoesNear, _echoRun) = (phraseStart == PhraseStart.GoesOn, null);

        int note;
        if (landing != ChordArrival.Free)
        {
            _echoRun = null;
            // a step down, to the chord's note a semitone or a whole tone below the last chord's landing, where one is in
            // range
            var below = landing == ChordArrival.StepDown && _landed is { } last
                ? chordToneClasses.Select(x => last - (last - x).Mod(OctaveNoteCount)).Where(x => last - x is 1 or 2 && x >= _low).Cast<int?>().Max()
                : null;
            var landingStep = landing switch
            {
                ChordArrival.Root or ChordArrival.StepDown => 0,
                ChordArrival.Third => 2,
                ChordArrival.Fifth => 4,
                _ => throw new ArgumentOutOfRangeException(nameof(landing), landing, null)
            };
            var pitchClass = chord.GetPitch(landingStep).Mod(OctaveNoteCount);
            // an inversion on a note its chord leaves out, a sus chord's third, on its root instead
            if (chordToneClasses.Count > 0 && !chordToneClasses.Contains(pitchClass))
                pitchClass = chord.GetPitch(0).Mod(OctaveNoteCount);
            note = below ?? GetNearest(Enumerable.Range(_low, _high - _low + 1).Where(x => x.Mod(OctaveNoteCount) == pitchClass).ToArray(), _previous ?? _middle + register * _profile.ContourShare);
            _landed = note;
        }
        else if (echo != 0 && _heard.TryGetValue(echo, out var heard))
        {
            // moved by as many scale steps as the phrase now aims away from where it aimed when the note was heard, so that
            // a figure repeated where its phrase rises or falls rises or falls with it, as a sequence, and one repeated
            // where the phrase aims as it did plays as heard, as a section that comes back does
            var moved = (int)Math.Round((aim - heard.Aim) * Scales.StepCount / OctaveNoteCount);
            // over the root it was heard over, as it was, but in a phrase that goes on, where that would leap from the
            // note before; over another root, nearest the note before, as a sequence: the octave the figure as heard
            // plays in, which the move by the aim then takes up or down, rather than one nearest the note before again,
            // which would take the move back
            if (_echoRun?.Root != chord.Root)
            {
                var asHeard = chord.Root == heard.Root && !(_echoesNear && _previous is { } last && Math.Abs(chord.GetPitch(heard.Step) - last) >= _profile.LeapSize);
                _echoRun = (chord.Root, asHeard ? 0 : GetNearestOctave(chord, heard.Step) - heard.Step);
            }

            // its step as heard
            var echoStep = heard.Step + moved;
            note = PlaceEcho(chord, chordToneClasses, beatRank, echoStep + _echoRun.Value.Octave);
        }
        else
        {
            _echoRun = null;
            note = pedal < 1 ? PlaceOnPedal(chord, beatRank, pedal) ?? PlaceByRule(chord, [], beatRank, step, turn, register)
                : PlaceByRule(chord, chordToneClasses, beatRank, step, turn, register);
        }

        // a droning line's melody an octave or more over its open string, as far as its instrument reaches, as a guitarist
        // leaps from the string to the high frets and back
        if (drones)
            while (note < OpenString(chord) + OctaveNoteCount && note + OctaveNoteCount <= _instrument.High)
                note += OctaveNoteCount;

        // the first time a note is heard it is remembered, as the step from its chord's root it has
        if (echo != 0)
            _heard.TryAdd(echo, (LinePlacement.GetScaleStep(chord, note), chord.Root, aim));

        _previousMove = _previous is { } before ? note - before : 0;
        if (_previousMove != 0)
            _heading = Math.Sign(_previousMove);
        _previous = note;
        return note;
    }

    /// <summary>
    ///     The note an appoggiatura resolves to: the chord's note a step (one or two semitones) from the line's last, below it
    ///     where one is, as a leaning note falls to its chord, and the nearer; none where no note of the chord in the range is
    ///     a step away, or the line has no note before.
    /// </summary>
    /// <param name="chordToneClasses">The pitch classes of the chord's notes.</param>
    /// <param name="lift">How far the note's range is lifted (<see cref="Place" />).</param>
    public int? Resolve(IReadOnlyCollection<int> chordToneClasses, double lift)
    {
        Lift(lift);
        if (_previous is not { } last
            || new[] { -1, -2, 1, 2 }.Select(x => last + x).Where(x => x >= _low && x <= _high && chordToneClasses.Contains(x.Mod(OctaveNoteCount))).Select(x => (int?)x).FirstOrDefault() is not { } note)
            return null;
        _echoRun = null;
        _previousMove = note - last;
        _heading = Math.Sign(_previousMove);
        _previous = note;
        return note;
    }

    /// <summary>The scale step, an octave's steps from the one given, whose note is nearest the note before.</summary>
    private int GetNearestOctave(ChordContext chord, int step)
    {
        var target = _previous ?? _middle;
        var steps = Enumerable.Range(-3, 7).Select(x => step + x * Scales.StepCount).ToArray();
        var inRange = steps.Where(x => chord.GetPitch(x) >= _low && chord.GetPitch(x) <= _high).ToArray();
        return (inRange.Length > 0 ? inRange : steps).MinBy(x => Math.Abs(chord.GetPitch(x) - target));
    }

    /// <summary>
    ///     An echo: the note of the scale step, from the chord's root, the line's nearest in the range if it leaves it, and
    ///     on a strong beat, which takes a note of the chord as it would by the rules, the note of the chord nearest it if
    ///     it misses the chord.
    /// </summary>
    private int PlaceEcho(ChordContext chord, IReadOnlyCollection<int> chordToneClasses, int beatRank, int step)
    {
        var note = chord.GetPitch(step);
        if (note < _low || note > _high)
            note = GetNearest(GetLineNotes(chord), note);
        var isStrong = beatRank <= _profile.StrongestWeakRank && chordToneClasses.Count > 0;
        if (isStrong && !chordToneClasses.Contains(note.Mod(OctaveNoteCount)))
            note = GetNearest(GetChordTones(chordToneClasses), note);
        // an echo off the line's scale moves to the nearest note it takes
        if (!Takes(note))
            note = GetNearest(Allowed(isStrong ? GetChordTones(chordToneClasses) : GetLineNotes(chord)), note);

        return note;
    }

    /// <summary>The singable range in the middle of a track's, or all of the track's if it is narrower.</summary>
    internal static (int Low, int High) GetSingableRange(int minNote, int maxNote, int rangeWidth)
    {
        var middle = (minNote + maxNote) / 2.0;
        var low = Math.Max(minNote, (int)Math.Round(middle - rangeWidth / 2.0));
        return (low, Math.Min(maxNote, low + rangeWidth));
    }

    /// <summary>
    ///     The note that leads into the next, where a chord changes, as the approach has it, aimed at where the next note
    ///     was placed, so that it never leaps an octave: by a scale step, the note as it is, if it is a step (one or two
    ///     semitones) from the next already, or the note nearest it that is, a step from it at most, so that it bends
    ///     rather than jumps, a note of its line's scale on a weak beat and of its chord on a strong one, where it would sound
    ///     against its own chord, or the note as it is, where no note is; a semitone below or above the next note, the next
    ///     chord's fifth nearest its root nearest the next note, or the next note itself, a little early.
    /// </summary>
    /// <param name="chordToneClasses">The pitch classes of the note's own chord's notes.</param>
    /// <param name="beatRank">How strong the note's beat is, 0 the strongest.</param>
    /// <param name="nextChord">The chord of the next note.</param>
    /// <param name="lineScale">The scale the note's line keeps to.</param>
    /// <param name="lift">How far the note's range is lifted, as it was placed (<see cref="Place" />).</param>
    public int Approach(
        ChordApproach approach,
        ChordContext chord,
        IReadOnlyCollection<int> chordToneClasses,
        int beatRank,
        int note,
        ChordContext nextChord,
        int next,
        LineScale lineScale,
        double lift
    )
    {
        Lift(lift);
        _classes = LineScales.Classes(lineScale, chord);
        var root = NearestOfClass(nextChord.Root, next);
        var approached = approach switch
        {
            ChordApproach.None => note,
            ChordApproach.ScaleStep => ApproachByStep(chord, chordToneClasses, beatRank, note, next),
            ChordApproach.HalfStepBelow => next - 1,
            ChordApproach.HalfStepAbove => next + 1,
            // the next chord's fifth where the line's scale over this chord has it, as a chromatic chord's may not, sounding
            // before its chord over another; a step from the side the line comes from where not
            ChordApproach.Fifth => InScale(chord, nextChord.GetPitch(4)) ? NearestOfClass(nextChord.GetPitch(4), root) : ApproachByStep(chord, chordToneClasses, beatRank, note, next),
            ChordApproach.Anticipation => next,
            _ => throw new ArgumentOutOfRangeException(nameof(approach), approach, null)
        };
        // a half step past the range's edge taken from the next note's other side, a half step still; another approach past
        // it moves an octave into it, as close to the edge as it can
        if (approach is ChordApproach.HalfStepBelow or ChordApproach.HalfStepAbove && (approached < _low || approached > _high))
            approached = 2 * next - approached;
        while (approached < _low)
            approached += OctaveNoteCount;
        while (approached > _high)
            approached -= OctaveNoteCount;
        return approached;
    }

    /// <summary>Whether a note is in the scale the line keeps over a chord: its line scale's, or the chord's own scale's.</summary>
    private bool InScale(ChordContext chord, int note) =>
        (_classes ?? Enumerable.Range(0, Scales.StepCount).Select(x => chord.GetPitch(x).Mod(OctaveNoteCount)).ToHashSet()).Contains(note.Mod(OctaveNoteCount));

    /// <summary>The note of the given one's pitch class nearest the other, the lower on a tie.</summary>
    private static int NearestOfClass(int note, double other)
    {
        var below = (int)Math.Floor(other) - ((int)Math.Floor(other) - note).Mod(OctaveNoteCount);
        return other - below <= below + OctaveNoteCount - other ? below : below + OctaveNoteCount;
    }

    private int ApproachByStep(ChordContext chord, IReadOnlyCollection<int> chordToneClasses, int beatRank, int note, int next)
    {
        static bool IsStep(int a, int b) => Math.Abs(a - b) is 1 or 2;
        if (IsStep(note, next))
            return note;

        var isStrong = beatRank <= _profile.StrongestWeakRank && chordToneClasses.Count > 0;
        var candidates = (isStrong ? GetChordTones(chordToneClasses) : GetLineNotes(chord))
            .Where(x => IsStep(x, next) && Math.Abs(x - note) <= 4)
            .ToArray();
        return candidates.Length == 0 ? note : GetNearest(candidates, note);
    }

    /// <summary>
    ///     How many of a chugging line's notes off its accents sit on the pedal, its chord's root at the bottom of its range,
    ///     the rest and the accents moving as its scale lets them, a chromatic stab among them where its scale is chromatic.
    /// </summary>
    public const double PedalShare = 0.7;

    /// <summary>
    ///     A chugging line's note: off its accents, its beats, the pedal, its chord's root the lowest in its range, where the
    ///     note's draw falls under the share; none for an accent or a note off it, which moves by its scale alone.
    /// </summary>
    /// <param name="draw">The note's own draw, from 0 to 1, as its key gives it.</param>
    private int? PlaceOnPedal(ChordContext chord, int beatRank, double draw)
    {
        if (beatRank == 0 || draw >= PedalShare)
            return null;
        var root = chord.Root.Mod(OctaveNoteCount);
        return Enumerable.Range(_low, _high - _low + 1).FirstOrDefault(x => x.Mod(OctaveNoteCount) == root, _low);
    }

    /// <summary>A droning line's open string: the key's tonic, the scale's first note, the lowest its instrument plays, as a low string sits under a riff's frets.</summary>
    private int OpenString(ChordContext chord) => _instrument.Low + (chord.Tonic - _instrument.Low).Mod(OctaveNoteCount);

    private int PlaceByRule(ChordContext chord, IReadOnlyCollection<int> chordToneClasses, int beatRank, int step, double turn, double register)
    {
        var aim = _middle + register * _profile.ContourShare;
        var isStrong = beatRank <= _profile.StrongestWeakRank && chordToneClasses.Count > 0;

        int note;
        if (_previous is not { } previous)
        {
            note = GetNearest(Allowed(isStrong ? GetChordTones(chordToneClasses) : GetLineNotes(chord)), aim);
        }
        else
        {
            // on the way the melody goes, or back, leaning towards the aim
            var goesOn = turn < _profile.GetContinueChance((aim - previous) * _heading);
            var direction = step == 0 ? 0 : goesOn ? _heading : -_heading;
            var isLeap = step >= 2;
            if (Math.Abs(_previousMove) >= _profile.LeapSize)
            {
                // a leap is followed by a step back
                direction = -Math.Sign(_previousMove);
                isLeap = false;
            }
            else if (Math.Abs(previous - aim) > _profile.RegisterPull)
            {
                direction = Math.Sign(aim - previous);
            }

            // a strong beat moves among all the chord's notes, as the line would, and then off the notes its scale leaves out
            // to the nearest other, so that it moves as far as it would; a weak one only among the notes of its scale
            var candidates = isStrong ? GetChordTones(chordToneClasses) : GetLineNotes(chord);
            note = GetNext(candidates, previous, direction, isLeap ? 2 : 1);
            if (isStrong && !Takes(note))
                note = GetNearest(Allowed(candidates), note);
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

    private bool Takes(int note) => _classes is null || _classes.Contains(note.Mod(OctaveNoteCount));

    /// <summary>The candidates the line's scale takes, or all of them where it takes none.</summary>
    private IReadOnlyList<int> Allowed(IReadOnlyList<int> candidates)
    {
        if (_classes is null)
            return candidates;

        var allowed = candidates.Where(Takes).ToArray();
        return allowed.Length > 0 ? allowed : candidates;
    }

    /// <summary>The notes of the line's scale in the range: the section's scale's, counted from the chord's root, or its own.</summary>
    private IReadOnlyList<int> GetLineNotes(ChordContext chord)
    {
        return _classes is null ? GetScaleNotes(chord) : Enumerable.Range(_low, _high - _low + 1).Where(Takes).ToArray();
    }

    /// <summary>The notes of the section's scale in the range, counted from the chord's root.</summary>
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

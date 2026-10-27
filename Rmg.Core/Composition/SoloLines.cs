using System.Collections.Immutable;
using Rmg.Core.Events;
using Rmg.Core.Probabilities;
using Rmg.Core.Songs;

namespace Rmg.Core.Composition;

/// <summary>The shape a solo's phrase's intensity takes over it.</summary>
public enum PhraseContour
{
    /// <summary>Up to its peak in its middle and down.</summary>
    RiseAndFall,

    /// <summary>Up to its peak at its end.</summary>
    Rise,

    /// <summary>From its peak down.</summary>
    Fall
}

/// <summary>What a soloist plays from one point of its solo to the next.</summary>
public enum SoloGesture
{
    /// <summary>A stream of notes at one speed, by the scale, to where the next gesture starts.</summary>
    Run,

    /// <summary>A short figure about a note, played again.</summary>
    Lick,

    /// <summary>A note held, for a bend or a vibrato.</summary>
    Held,

    /// <summary>Nothing, a breath.</summary>
    Pause,

    /// <summary>The melody's notes there, as the soloist plays them.</summary>
    Quote,

    /// <summary>A note or two struck on the band's own onsets there, short, a funk horn's or guitarist's riff on the groove.</summary>
    Rhythm
}

/// <summary>
///     A solo's line, phrase by phrase and gesture by gesture (<see cref="Plan" />): phrases of the soloist's lengths, their
///     peaks building to the solo's climax and easing after, each ending on a release, a note of the chord held from a
///     strong beat; within a phrase gestures of lengths of their own, picked by its intensity there, a run the likelier the
///     more intense, a held note or a pause the less, from the soloist's vocabulary (<see cref="Soloist" />), a run going
///     on over the bar lines in the phrase. A new run sets out the way to where the gesture ends, without aiming to land
///     there: a note of the chord there, higher the more intense. A solo on the melody plays the tune's every note instead,
///     accented, as it was sung, or with a steady fill of 8ths between them where it rolls. Every note is a scale note in the soloist's range, as its chord there
///     has it, kept as its scale step over that chord, so that a run crossing a change of chord stays on the scale.
/// </summary>
internal static class SoloLines
{
    /// <summary>
    ///     The gestures, their weights by the soloist's facet, the runs the heavier and the held notes and the pauses the
    ///     lighter the wilder, and how each leans with the intensity, from the calmest to the most intense; a quote's weight
    ///     is what there is to quote.
    /// </summary>
    public static ImmutableArray<(SoloGesture Gesture, ByConvention Weight, double Lean)> Gestures { get; } =
    [
        (SoloGesture.Run, new ByConvention(0.6, 2.2, 2.5), 1),
        (SoloGesture.Lick, new ByConvention(0.8, 1, 1), 0.3),
        (SoloGesture.Quote, new ByConvention(1, 1, 1), 0),
        (SoloGesture.Held, new ByConvention(1.2, 0.5, 0.4), -0.6),
        (SoloGesture.Pause, new ByConvention(1, 0.4, 0.3), -1),
        // a soloist's who riffs alone (Soloist.Riffs)
        (SoloGesture.Rhythm, new ByConvention(1.6, 1.6, 1.6), 0.2)
    ];

    /// <summary>How much a quote of the melody weighs among a solo's gestures.</summary>
    public const double QuoteWeight = 0.15;

    /// <summary>A beat's places, which a solo note's draws are keyed by.</summary>
    private const int PlacesPerBeat = 48;

    /// <summary>
    ///     How far apart a solo on the melody's fill strikes between the tune's notes, in beats, an 8th, as a picker's roll
    ///     keeps going, and how near a tune's note it strikes at the closest.
    /// </summary>
    public const double FillStep = 0.5, FillGap = 0.25;

    /// <summary>How much softer a solo on the melody's fill plays than the tune it rolls around, as a velocity sum, so that the tune is heard through it.</summary>
    public const double FillSofter = 0.4;

    /// <summary>The odds the most intense gesture has over the calmest at an intensity of 1, and the other way at 0.</summary>
    public const double IntensityOdds = 16;

    /// <summary>How much a run leans to the soloist's faster speeds beyond the intensity, as the odds of 1, a run being a soloist's fastest playing.</summary>
    public const double RunSpeedOdds = 3;

    /// <summary>
    ///     The fastest a soloist plays but in a burst, in notes a beat, and how intense a segment is at least to burst faster,
    ///     as a player saves 32nds for the peak, a run of them through a whole solo blurring.
    /// </summary>
    public const int BurstSpeed = 8;

    public const double BurstIntensity = 0.8;

    /// <summary>
    ///     How far before its bar a pickup starts, in beats, as likely each; how much of the rest before it it leaves at least,
    ///     in beats; and how fast it goes at most, in notes a beat: 8ths or 16ths leading in.
    /// </summary>
    private static readonly double[] PickupLeads = [0.5, 1, 1.5];

    private const double PickupRoom = 0.5;

    private const int PickupSpeed = 4;

    /// <summary>A spacious soloist's line, in bars, its section's phrase where that is not a multiple of it, and its call's.</summary>
    private const int SpaceLineBars = 4, SpaceCallBars = 1;

    /// <summary>
    ///     How long a solo's chorus is at least, in bars, to build in each of several, and how much of its own arc the whole
    ///     solo's leaves its quietest chorus.
    /// </summary>
    private const int WaveBars = 8;

    private const double WaveFloor = 0.7;

    /// <summary>
    ///     How many of the band's onsets a riff on them needs at least, how long its note is at most, in beats, and how much of
    ///     the time to the next onset it sounds, short, as struck.
    /// </summary>
    private const int RhythmOnsets = 2;

    private const double RhythmNote = 0.5, RhythmShare = 0.5;

    /// <summary>How much later a displaced motif plays, in beats, as likely each: an 8th, or a beat.</summary>
    private static readonly double[] DisplaceShifts = [0.5, 1];

    /// <summary>
    ///     The solo's arc, which its phrases' peaks follow: where it starts, the climax it rises to at about 70% of the way,
    ///     and where it eases to by its end.
    /// </summary>
    public const double ArcStart = 0.3;

    public const double ClimaxAt = 0.7;

    public const double ArcEnd = 0.6;

    /// <summary>The solo's arc a share of the way through it: up from its start to the climax, and easing after.</summary>
    public static double Arc(double place) => place < ClimaxAt ? ArcStart + (1 - ArcStart) * place / ClimaxAt : 1 - (1 - ArcEnd) * (place - ClimaxAt) / (1 - ClimaxAt);

    /// <summary>How far the soloist's volatility moves a phrase's peak and a gesture's intensity about.</summary>
    public const double Drift = 0.4;

    /// <summary>The chance a gesture's intensity is thrown anywhere, times the soloist's volatility, about 0.7 at the most.</summary>
    public const double JumpRate = 0.5;

    /// <summary>The shape a phrase's intensity takes, up to its peak and down, how likely each is.</summary>
    public static ImmutableArray<Weighted<PhraseContour>> Contours { get; } =
    [
        new(0.5, PhraseContour.RiseAndFall),
        new(0.3, PhraseContour.Rise),
        new(0.2, PhraseContour.Fall)
    ];

    /// <summary>A phrase's intensity a share of the way through it, as a share of its peak.</summary>
    public static double Contour(PhraseContour contour, double share) => contour switch
    {
        PhraseContour.RiseAndFall => 0.4 + 0.6 * Math.Sin(Math.PI * share),
        PhraseContour.Rise => 0.4 + 0.6 * share,
        PhraseContour.Fall => 1 - 0.6 * share,
        _ => throw new ArgumentOutOfRangeException(nameof(contour), contour, null)
    };

    /// <summary>How long a gesture lasts, in beats, and how it leans with the intensity: the longer the more intense.</summary>
    public static ImmutableArray<(double Beats, double Lean)> GestureLengths { get; } = [(1, -1), (2, 0), (4, 1)];

    /// <summary>
    ///     A shape's cell, as the scale steps its notes are from the cell's first, its figure going round for as many notes as
    ///     the cell has, and how far the next cell starts on, the way the run goes: a scale's straight on, a sequence's and a
    ///     turn's a step, an arpeggio's, the scale's thirds stacked from wherever it is, a third, a pedal's under its moving
    ///     notes a step.
    /// </summary>
    public static (int[] Figure, int Advance, bool Continues) Shape(SoloShape shape) => shape switch
    {
        SoloShape.Scale => ([0, 1, 2, 3, 4, 5, 6, 7, 8], 1, true),
        SoloShape.Sequence => ([0, 1, 2, 3, 4, 5, 6, 7, 8], 1, false),
        SoloShape.Arpeggio => ([0, 2, 4, 7, 9, 11, 14, 16, 18], 2, false),
        SoloShape.Turn => ([0, 1, 0, -1], 1, false),
        SoloShape.Pedal => ([0, 1, 0, 2, 0, 3, 0, 4, 0], 1, false),
        _ => throw new ArgumentOutOfRangeException(nameof(shape), shape, null)
    };

    /// <summary>How much louder a solo's phrase plays at its highest note than at its lowest, as the melody's does (<see cref="MelodyLayers" />), before its genre's evenness (<see cref="Soloist.PhraseArc" />).</summary>
    public const double PhraseArc = 0.2;

    /// <summary>How much louder a solo plays at its most intense than at its calmest, about the melody's loudness there.</summary>
    public const double IntensityVelocity = 0.15;

    /// <summary>
    ///     How each gesture leans a soloist to stacking it: a held note the most, a lick or a quote as it is, a run the least,
    ///     as a run in chords blurs; a stacked run or lick stacks only its cells' first notes, a held note or a quote every one.
    /// </summary>
    public static ImmutableDictionary<SoloGesture, double> ChordLeans { get; } = new Dictionary<SoloGesture, double>
    {
        [SoloGesture.Run] = -1,
        [SoloGesture.Lick] = 0,
        [SoloGesture.Quote] = 0,
        [SoloGesture.Held] = 1,
        [SoloGesture.Pause] = 0,
        [SoloGesture.Rhythm] = 0
    }.ToImmutableDictionary();

    /// <summary>The odds a gesture leaning to stacking has over one leaning away, and an intense moment over a calm one, as the odds of 1.</summary>
    public const double ChordOdds = 3;

    /// <summary>
    ///     The chance a phrase's first run or lick opens on the phrase before's first cell moved a scale step, up or down, as
    ///     a soloist answers its own idea: neither plainer nor wilder.
    /// </summary>
    public const double MotifChance = 0.5;

    /// <summary>How much louder a cell's first note is, so that its grouping is heard.</summary>
    public const double CellAccent = 0.1;

    /// <summary>How often a ghosting soloist mutes an unaccented note of a lick at 16ths or slower (<see cref="Soloist.Ghosts" />).</summary>
    public const double GhostRate = 0.2;

    /// <summary>The stream a lick's note draws whether it is a ghost from, by its key.</summary>
    private const int GhostStream = 1;

    /// <summary>How far a soloist's register reaches either side of its middle, in semitones.</summary>
    private static int Reach(Soloist soloist) => (int)Math.Round(soloist.RegisterSteps * 12 / 14.0);

    /// <summary>
    ///     The octaves a soloist's part plays its solo above its own range, high in its instrument's whatever part plays it,
    ///     as a guitarist solos up the neck (<see cref="LeadShare" />): those that bring its range's middle nearest the lead
    ///     where it lies under it, as a riff's tuned down does, but never its own top past the instrument's; played up by
    ///     them (<see cref="CompositionStateKinds.LineRaise" />).
    /// </summary>
    internal static int RaiseOf(PitchInstrumentTrack definition)
    {
        var (ownLow, ownHigh) = Realizer.GetRange(definition);
        var (instrumentLow, instrumentHigh) = Registers.RangeOf(definition);
        var lead = instrumentLow + (int)Math.Round(LeadShare * (instrumentHigh - instrumentLow));
        return Math.Clamp((int)Math.Round((lead - (ownLow + ownHigh) / 2.0) / 12), 0, Math.Max(0, (instrumentHigh - ownHigh) / 12));
    }

    /// <summary>The octaves a part plays a line up by to play it in another's register, raised as that one is, but never its own top past its instrument's.</summary>
    internal static int RaiseTo(PitchInstrumentTrack definition, PitchInstrumentTrack other, int otherRaise)
    {
        var ((_, ownHigh), (_, otherHigh)) = (Realizer.GetRange(definition), Realizer.GetRange(other));
        return Math.Clamp((int)Math.Round((otherHigh + 12 * otherRaise - ownHigh) / 12.0), 0, Math.Max(0, (Registers.RangeOf(definition).High - ownHigh) / 12));
    }

    /// <summary>How high in its instrument's range a soloist leads, as a share of it from its lowest note: two thirds up, a guitar's solo up the neck, a horn's in its bright upper register.</summary>
    public const double LeadShare = 0.65;

    /// <summary>The stream a run's note on a change draws whether it lands there from, by its key (<see cref="Soloist.LandingChances" />).</summary>
    private const int LandingStream = 2;

    /// <summary>How many scale steps a run's note on a change moves to land at most: one to a triad's note, two to a power chord's.</summary>
    private const int LandingSteps = 2;

    /// <summary>The place a note's draw of whether it approaches its next chromatically is derived from its key by, a sequence of its own.</summary>
    private const int ChromaticStream = 3;

    /// <summary>The steps above a chord's root a triad's notes are, the root, the third and the fifth, where no part carries the chord's shape.</summary>
    private static readonly int[] ChordSteps = [0, 2, 4];

    /// <summary>A solo's line, and every segment's intensity and gesture, by its start.</summary>
    /// <param name="melody">The melody's notes in the solo, which a solo on the melody aims at and quotes, and every note takes its state from.</param>
    /// <param name="tune">The tune a solo on the melody plays, as an appearance before sang it, moved to the solo: its notes' starts, pitches and held lengths; none for a solo of its own.</param>
    /// <param name="rolls">Whether a solo on the melody rolls 8ths of its chords round the tune, a picker's break, rather than playing it as sung.</param>
    /// <param name="band">The chords' notes in the solo, the band's chord a stacked note stacks; the melody's where they rest.</param>
    /// <param name="chordAt">The chords part's state at a place, as it strikes its chord there (<see cref="Realizer.ChordStateAt" />), the chord's own notes; none where it strikes none.</param>
    /// <param name="groove">Where the band strikes in the solo, the rhythm part's onsets or, where it has none, the chords', which a riff strikes on.</param>
    /// <param name="turn">How long a soloist's turn is where several trade, which no phrase runs over; the span's length where none do.</param>
    /// <param name="barOrigin">Where the bars the span plays in start, its section's start (<see cref="Plan" />).</param>
    /// <param name="climbs">Whether the line's range opens with the solo's arc (<see cref="Soloist.Ceiling" />), a solo's own; an answer's, which has no arc, and a solo on the melody's, which sings where the melody does, whole.</param>
    /// <param name="track">The soloist's track, whose state its notes are realized over.</param>
    /// <param name="definition">The soloist's definition, its range.</param>
    /// <param name="voicing">The sequence that draws which gestures the soloist stacks, a stream of its own.</param>
    /// <param name="motifs">The sequence that draws which phrases open on the phrase before's motif, a stream of its own.</param>
    /// <param name="pickups">The sequence that draws which phrases come in on a pickup, a stream of its own.</param>
    public static (TimelineItem<StateMap>[] Line, Segment[] Segments) Generate(
        IGenerationContext context,
        IGenerationContext voicing,
        IGenerationContext motifs,
        IGenerationContext pickups,
        SectionSpan span,
        double turn,
        double barOrigin,
        Soloist soloist,
        bool isOwn,
        bool climbs,
        IReadOnlyList<TimelineItem<StateMap>> melody,
        IReadOnlyList<(double Position, int Pitch, double Held)> tune,
        bool rolls,
        IReadOnlyList<TimelineItem<StateMap>> band,
        Func<double, StateMap?> chordAt,
        IReadOnlyList<double> groove,
        EventStateTimelineMap<StateMap> track,
        PitchInstrumentTrack definition,
        StateTimelineMap common
    )
    {
        // the soloist's register, its range in scale steps about where its instrument leads, a whole step inside its edges,
        // as a note kept as its scale step moves by a step or so over the next chord and would fold an octave past one
        var reach = Reach(soloist);
        var raise = RaiseOf(definition);
        var (ownLow, ownHigh) = Realizer.GetRange(definition);
        var (instrumentLow, instrumentHigh) = Registers.RangeOf(definition);
        var lead = instrumentLow + (int)Math.Round(LeadShare * (instrumentHigh - instrumentLow));
        var (low, high) = (ownLow + 12 * raise, ownHigh + 12 * raise);
        var middle = Math.Clamp(lead, Math.Min(low + reach, (low + high) / 2), Math.Max(high - reach, (low + high) / 2));
        var bottom = Math.Max(low + 2, middle - reach);
        var top = Math.Max(bottom, Math.Min(Math.Min(high, instrumentHigh) - 2, middle + reach));

        // the top of its register there: where the line climbs, a share of it the soloist's first phrase opens rising with the
        // solo's arc to the whole at its climax, so that its highest note comes there, and never under an octave over its bottom
        // but within its register, which a narrow part's is
        int TopAt(double position) => !climbs ? top
            : Math.Min(top, Math.Max(bottom + 12, bottom + (int)Math.Round((top - bottom) * (soloist.Ceiling + (1 - soloist.Ceiling) * (Arc((position - span.Start) / span.Duration) - ArcStart) / (1 - ArcStart)))));

        // the chord a note plays over, as Realizer reads its state there
        ChordContext Chord(StateMap note, double position) => Realizer.ChordAt(note, position, track, definition, common);

        // the melody's note sounding at a position, the first before it where none does, whose state a note there takes
        TimelineItem<StateMap> MelodyAt(double position) => melody.LastOrDefault(x => x.Position <= position + 1e-9, melody[0]);
        StateMap BandAt(double position) => band.Any(x => x.Position <= position + 1e-9) ? band.Last(x => x.Position <= position + 1e-9).Value : MelodyAt(position).Value;
        int MelodyPitch(TimelineItem<StateMap> note) => LinePlacement.PitchOf(Chord(note.Value, note.Position), note.Value);
        int[] Notes(double position, IEnumerable<int> steps)
        {
            var chord = Chord(MelodyAt(position).Value, position);
            return [..steps.Select(chord.GetPitch).Where(x => x >= bottom && x <= TopAt(position)).Distinct().Order()];
        }

        // every octave a register may reach, a piccolo's lead high in its range as a bass's low in its own
        int[] ScaleNotes(double position) => Notes(position, Enumerable.Range(-4 * Scales.StepCount, 16 * Scales.StepCount));
        // the chord's own notes, as the band's shape has them, a sus or a power chord's without the third it leaves out; the
        // triad's where no part carries a shape there
        int[] ShapeSteps(double position) => chordAt(position) is { } state ? [..Realizer.GetChordNotes(state).Steps] : ChordSteps;
        var chordChanges = common.GetStateTimeline(StateKinds.ChordChange).Select(x => x.Position).ToArray();
        int[] ChordNotes(double position) => Notes(position, Enumerable.Range(-4, 16).SelectMany(octave => ShapeSteps(position).Select(x => x + octave * Scales.StepCount)));
        int Nearest(int[] notes, int pitch) => notes.Length == 0 ? Math.Clamp(pitch, bottom, top) : notes.MinBy(x => Math.Abs(x - pitch));

        // the louder the more intense, around none, as the caller sets how loud the line plays as heard (Solos.Lift)
        const double loudness = 0;

        // every note's pitch as planned, in the line's order, placed once the whole line is, so that a note may be moved for
        // what comes after it
        var planned = new List<PlannedNote>();
        var placing = 0;
        void Add(double position, int pitch, double intensity, double held = 0, bool accented = false, bool release = false, bool stacked = false, bool octave = false, bool ghosts = false, bool run = false, double lift = 0) =>
            planned.Add(new PlannedNote(position, pitch, intensity, held, accented, release, stacked, octave, ghosts, run, placing, false, lift));

        // a note's place in the section, in places a beat from 1, which its draws are keyed by, so that a note added or
        // left out before it, a pickup's or a displaced motif's, leaves them as they were
        long PlaceOf(double position) => (long)Math.Round((position - span.Start) * PlacesPerBeat) + 1;
        // a note's key, its melody note's and its place, which its own draws are made by
        long KeyOf(double position) => Seeds.Key(MelodyAt(position).Value.GetStateValue(CompositionStateKinds.NoteKey), PlaceOf(position));
        // a note's draw of a decision, by its key and the decision's stream
        bool Draws(double position, long stream, double chance) => new GenerationContext(Seeds.Derive((ulong)KeyOf(position), stream)).TestProbability(chance);

        // a note of the line at its place, as a line's note: the step above the chord there nearest it, on the scale there,
        // its key by its place
        TimelineItem<StateMap> Place(PlannedNote note)
        {
            var (position, pitch, intensity, held, accented, release, stacked, octave, _, _, _, altered, lift) = note;
            var sung = MelodyAt(position);
            // in octaves as the soloist plays them, not as the melody it takes over did; a note of its own, the melody's
            // beat where it starts with the melody's note, the weakest elsewhere, its own key from that note's and its place
            var state = sung.Value.Except([StateKinds.HeldDuration, CompositionStateKinds.LinePickup, StateKinds.ChordNotePitchOffsets, StateKinds.ChordVoicingFixed, CompositionStateKinds.Octave,
                    CompositionStateKinds.LineVoices, CompositionStateKinds.LinePlanes])
                .With(CompositionStateKinds.Octave, octave && soloist.Octaves ? 1 : 0)
                .With(CompositionStateKinds.BeatRank, Math.Abs(sung.Position - position) < 1e-9 ? sung.Value.GetStateValue(CompositionStateKinds.BeatRank) : DyadicRankDistribution.MaxRank)
                .With(CompositionStateKinds.NoteKey, KeyOf(position));
            state = state.With(StateKinds.Velocity, loudness + IntensityVelocity * (intensity - 0.5) + (accented ? CellAccent : 0) + lift);
            // a note off the scale the scale's step beside it, altered onto it, kept under the octaves the line is raised by,
            // which the note is played up by
            var step = LinePlacement.GetScaleStep(Chord(state, position), pitch - 12 * raise);
            state = state.With(StateKinds.ScaleStep, step)
                .With(StateKinds.Alteration, altered ? pitch - 12 * raise - Chord(state, position).GetPitch(step) : 0)
                .With(CompositionStateKinds.LineRaise, raise)
                .With(CompositionStateKinds.SoloIntensity, (int)Math.Round(intensity * 100))
                .With(CompositionStateKinds.SoloRelease, release ? 1 : 0);
            // a stacked note the band's chord there under it, or the chord's shape planed (Realizer): the chords' note sounding
            // there, as the melody's before it may be of a chord before
            if (stacked && soloist.Voices > 1)
                state = state.With(StateKinds.ChordNotePitchOffsets, BandAt(position).GetStateValue(StateKinds.ChordNotePitchOffsets))
                    .With(CompositionStateKinds.LineVoices, soloist.Voices)
                    .With(CompositionStateKinds.LinePlanes, soloist.Planes ? 1 : 0);

            var placed = (held > 0 ? state.With(StateKinds.HeldDuration, held) : state).ToTimelineItem(position);
            // a ghosting lick's unaccented note muted now and then, by its own key's draw
            return note.Ghosts && Draws(position, GhostStream, GhostRate) ? placed.Value.With(CompositionStateKinds.PalmMute, 1).ToTimelineItem(position) : placed;
        }

        var plan = Plan(
            context,
            pickups,
            span,
            soloist,
            // a quote wants the melody's notes there
            (start, end) => !melody.Any(n => n.IsWithin(start, end)) ? 0 : QuoteWeight,
            position => Meter.BeatsPerSecond(common.GetStateTimeline(StateKinds.Tempo).GetEffectiveValueAt(position)),
            turn,
            barOrigin
        );
        // whether a motif restated is displaced, from a sequence of its own after the plan's pickups
        var displacing = new GenerationContext(pickups.GenerateSeed());
        // a riff where the band gives it onsets enough, a lick where it holds, as the plan then has it
        double[] OnsetsIn(double start, double end) => [..groove.Where(x => x >= start - 1e-9 && x < end - 1e-9).Distinct().Order()];
        plan = [..plan.Select(x => x.Gesture == SoloGesture.Rhythm && OnsetsIn(x.Start, x.End).Length < RhythmOnsets ? x with { Gesture = SoloGesture.Lick } : x)];
        // the phrase of the solo's most intense moment, its climax
        var climax = plan.Length > 0 ? plan.MaxBy(x => x.Intensity)!.Phrase : -1;
        var current = Nearest(ChordNotes(span.Start), middle);
        CellCursor? cursor = null;
        // the phrase before's motif, its first run's or lick's first cell, as its first note and the scale steps of the others
        // from it, so that a scale over another chord takes its figure whole, and the phrase whose motif is taken
        (int First, int[] Steps)? motif = null;
        var (motifPhrase, motifShape) = (-1, SoloShape.Scale);
        // a solo of its own playing its gestures; one on the melody plays the tune instead
        foreach (var (start, end, level, gesture, speed, shape, sizes, goesOn, phrase, isRelease) in plan.Where(_ => isOwn))
        {
            placing = phrase;
            // whether the soloist stacks the gesture, the likelier the more it leans to it and the more intense
            var stacks = voicing.TestProbability(new Tilt(Tilt.Of(ChordOdds, ChordLeans[gesture]).LogOdds + Tilt.Of(ChordOdds, 2 * level - 1).LogOdds).Chance(soloist.ChordRate, 1));
            // a run going on from the segment before keeps where it is in its cells
            cursor = goesOn && cursor is not null ? cursor : new CellCursor(current);
            // and the way a new run sets out: towards a note of the chord where the gesture ends, higher the more intense
            var heading = Nearest(ChordNotes(Math.Min(end, span.End - 1e-6)), (current + bottom + (int)Math.Round(level * (TopAt(start) - bottom))) / 2);

            switch (gesture)
            {
                case SoloGesture.Held:
                    // the climax's phrase released on the highest note of the chord under the register's top there, the
                    // solo's peak held, as a rock player ends the build on a high bent note; the others the nearest
                    current = isRelease && phrase == climax && climbs ? ChordNotes(start).Where(x => x <= TopAt(start)).DefaultIfEmpty(Nearest(ChordNotes(start), current)).Max()
                        : Nearest(ChordNotes(start), current);
                    Add(start, current, level, end - start, release: isRelease, stacked: stacks, octave: true);
                    break;
                case SoloGesture.Run:
                case SoloGesture.Lick:
                {
                    // a lick at half the speed, its cell again in place, and a run its cells on the way to where it heads
                    var positions = Positions(start, end, gesture, speed);
                    var scale = ScaleNotes(start);
                    var (pitches, accents, cells) = Cells(scale, cursor, heading, positions.Length, shape, sizes, gesture == SoloGesture.Run);
                    var shift = 0.0;
                    // a phrase's first opening on the phrase before's motif a step away now and then, its own the next's
                    if (phrase != motifPhrase && pitches.Length > 0)
                    {
                        // a spacious soloist's answer always on its call's motif
                        var answers = motifs.TestProbability(MotifChance) || soloist.Spacious && phrase % 2 == 1;
                        var step = motifs.GenerateInt(0, 2) * 2 - 1;
                        var inverts = motifs.TestProbability(soloist.MotifInversion);
                        // in the motif's own figure, a scale's answered by a scale
                        int Index(int pitch) => Array.IndexOf(scale, Nearest(scale, pitch));
                        if (answers && motif is { } taken && shape == motifShape && scale.Length > 0)
                        {
                            // the motif in place of the first cell, a step away, or its intervals mirrored about its first
                            // note, developed rather than moved; and the rest moved with its last note, so that the gesture
                            // goes on from it as it went on from its own; not where it would leave the range, which the
                            // gesture keeps to as it is
                            int MotifAt(int i) => Index(taken.First) + (inverts ? -taken.Steps[i] : taken.Steps[i]);
                            var count = Math.Min(taken.Steps.Length, pitches.Length);
                            var moved = MotifAt(count - 1) + step - Index(pitches[count - 1]);
                            int[] answered = [..pitches.Select((x, i) => i < count ? MotifAt(i) + step : Index(x) + moved)];
                            var root = Index(cursor.Root) + moved;
                            if (answered.All(x => x >= 0 && x < scale.Length) && root >= 0 && root < scale.Length)
                            {
                                for (var i = 0; i < pitches.Length; i++)
                                    pitches[i] = scale[answered[i]];
                                // and a run going on into the next gesture goes on from where it was moved to
                                cursor.Root = scale[root];
                                // and played later by an 8th or a beat now and then, displaced, its rhythm as it was, its notes past
                                // the gesture's end left out, as a player moves a lick in the bar to make it new
                                // and a gesture long enough to keep half a beat of itself after it, drawn whatever it is
                                var (displaces, by) = (displacing.TestProbability(soloist.DisplaceChance), DisplaceShifts[displacing.GenerateInt(0, DisplaceShifts.Length)]);
                                if (displaces && end - start >= by + 0.5 - 1e-9)
                                    shift = by;
                                StateTrace.RecordSection(TracePoints.SoloMotif, SectionGenerator.SectionTrace, span.SectionId, $"{start}+{shift}", (start, shift));
                            }
                        }
                        var cell = pitches[..Math.Min(cells.Length > 0 ? cells[0] : pitches.Length, pitches.Length)];
                        motif = (cell[0], [..cell.Select(x => Index(x) - Index(cell[0]))]);
                        (motifPhrase, motifShape) = (phrase, shape);
                    }

                    if (shift > 0)
                    {
                        positions = [..positions.Select(x => x + shift).Where(x => x < end - 1e-9)];
                        (pitches, accents) = (pitches[..positions.Length], accents[..positions.Length]);
                        // and a run going on from it set out afresh from its last note kept, never from the notes left out
                        cursor = null;
                    }

                    // a run, too fast to play in octaves, alone; and a ghosting soloist's lick at 16ths or slower, not on a
                    // pedal, an unaccented note of it muted now and then, by its own key's draw
                    var ghosting = gesture == SoloGesture.Lick && soloist.Ghosts && shape != SoloShape.Pedal && (positions.Length < 2 || positions[1] - positions[0] >= 0.25 - 1e-9);
                    for (var i = 0; i < positions.Length; i++)
                        Add(positions[i], pitches[i], level, accented: accents[i], stacked: stacks && accents[i], octave: gesture == SoloGesture.Lick, ghosts: ghosting && !accents[i],
                            run: gesture == SoloGesture.Run);
                    StateTrace.RecordSection(TracePoints.SoloCells, SectionGenerator.SectionTrace, span.SectionId, $"{gesture} {shape} {string.Join("+", cells)}", (start, shape, cells));
                    if (pitches.Length > 0)
                        current = pitches[^1];
                    break;
                }
                // a riff on the band's onsets there, a chord's note near the line and the scale's note above it, the first
                // accented and every note short
                case SoloGesture.Rhythm:
                {
                    var onsets = OnsetsIn(start, end);
                    current = Nearest(ChordNotes(start), current);
                    var scale = ScaleNotes(start);
                    var above = scale.FirstOrDefault(x => x > current, current);
                    for (var i = 0; i < onsets.Length; i++)
                    {
                        var length = (i + 1 < onsets.Length ? onsets[i + 1] : end) - onsets[i];
                        Add(onsets[i], i % 3 == 2 ? above : current, level, Math.Min(RhythmNote, length * RhythmShare), accented: i == 0);
                    }

                    break;
                }
                case SoloGesture.Quote:
                    foreach (var note in melody.Where(x => x.IsWithin(start, end)))
                    {
                        current = Nearest(ScaleNotes(note.Position), FoldTowards(MelodyPitch(note), current));
                        Add(note.Position, current, level, stacked: stacks, octave: true);
                    }

                    break;
            }
        }

        // a solo on the melody playing the tune whole, every note of it accented and moved by the octaves that bring the
        // tune's middle nearest the part's, so that it keeps its shape, a note that would leave the part's range folded back
        // into it; each held as it was sung, its rests left to the band, or where it rolls, between its notes and before its
        // first, a fill on the 8ths of the bar, as a picker rolls through a break, each of the chord's notes there nearest the
        // way from the tune's note to its next, never the one before again, and the last a scale step beside the next,
        // leading into it
        if (!isOwn && tune.Count > 0)
        {
            Segment? SegmentAt(double position) => plan.LastOrDefault(x => x.Start <= position + 1e-9);
            void Play(double position, int pitch, bool accented, double held = 0)
            {
                placing = SegmentAt(position)?.Phrase ?? 0;
                Add(position, pitch, SegmentAt(position)?.Intensity ?? 0.5, held, accented: accented, octave: accented, lift: accented ? 0 : -FillSofter);
            }

            var shift = 12 * (int)Math.Round((middle - (tune.Min(x => x.Pitch) + tune.Max(x => x.Pitch)) / 2.0) / 12);
            int Moved(int pitch) => Realizer.IntoRange(pitch + shift, low, high);

            (double Position, int Pitch, double Held)[] notes = [..tune.Select(x => (x.Position, Moved(x.Pitch), x.Held))];
            void Fill(double from, int pitch, double to, int? next)
            {
                var previous = pitch;
                // the 8ths of every bar it fills, each bar's counted from its own start, as an odd meter's bar ends on a 16th
                var bar = span.Meter.BarDuration;
                var positions = new List<double>();
                for (var barStart = barOrigin + Math.Floor((from + FillGap - barOrigin) / bar + 1e-9) * bar; barStart < to - FillGap + 1e-9; barStart += bar)
                    for (var position = barStart; position < Math.Min(barStart + bar - 1e-9, to - FillGap + 1e-9); position += FillStep)
                        if (position >= from + FillGap - 1e-9)
                            positions.Add(position);
                foreach (var (position, k) in positions.Select((x, k) => (x, k)))
                {
                    var target = next ?? pitch;
                    int filled;
                    if (next is not null && k == positions.Count - 1)
                    {
                        // the last, a scale step beside the next, from the side the fill comes from
                        var scale = ScaleNotes(position);
                        var at = Array.IndexOf(scale, Nearest(scale, target));
                        var beside = previous > target ? at + 1 : at - 1;
                        filled = scale.Length == 0 ? target : scale[Math.Clamp(beside, 0, scale.Length - 1)];
                    }
                    else
                    {
                        var share = (position - from) / (to - from);
                        var towards = (int)Math.Round(pitch + (target - pitch) * share);
                        var chord = ChordNotes(position).Where(x => x != previous).ToArray();
                        filled = Nearest(chord, towards);
                    }

                    Play(position, filled, false);
                    previous = filled;
                }
            }

            if (rolls)
                Fill(span.Start - FillGap, notes[0].Pitch, notes[0].Position, notes[0].Pitch);
            for (var i = 0; i < notes.Length; i++)
            {
                Play(notes[i].Position, notes[i].Pitch, true, rolls ? 0 : notes[i].Held);
                // a note off the scale here, a blue note, altered as it was sung
                var chord = Chord(MelodyAt(notes[i].Position).Value, notes[i].Position);
                planned[^1] = planned[^1] with { Altered = chord.GetPitch(LinePlacement.GetScaleStep(chord, notes[i].Pitch)) != notes[i].Pitch };
                if (rolls)
                    Fill(notes[i].Position, notes[i].Pitch, i + 1 < notes.Length ? notes[i + 1].Position : span.End, i + 1 < notes.Length ? notes[i + 1].Pitch : null);
            }

            StateTrace.RecordSection(TracePoints.SoloTune, SectionGenerator.SectionTrace, span.SectionId, $"{notes.Length} notes", notes);
            planned.Sort((a, b) => a.Position.CompareTo(b.Position));
        }

        // a run's note on a change's downbeat landing there now and then, by its own key's draw, the key it is placed with
        bool Lands(int k) => Draws(planned[k].Position, LandingStream, soloist.LandingChance);

        // a run's note on a change's downbeat landed on the new chord's nearest note, a scale step or two away, the way the run
        // goes where two are as near: the run moved by as many steps from it on, leaping onto the chord's note as a player does
        // and going on from there, or, where that would leave the range, the note's cell from it on, or the cell whole, which
        // keeps a figure's shape; the next nearest where none can, and none where every move would leave the range or strike
        // a note twice running
        void Land(int k)
        {
            var note = planned[k];
            var scale = ScaleNotes(note.Position);
            if (ChordNotes(note.Position).Contains(note.Pitch) || scale.Length == 0)
                return;
            // the run the note is of, a run's notes going on in its phrase, and the cell it is in, from its accented first note
            bool OfRun(int i) => i >= 0 && i < planned.Count && planned[i].Run && planned[i].Phrase == note.Phrase;
            var (cellStart, cellEnd, runEnd) = (k, k, k);
            while (OfRun(cellStart - 1) && !planned[cellStart].Accented)
                cellStart--;
            while (OfRun(cellEnd + 1) && !planned[cellEnd + 1].Accented)
                cellEnd++;
            while (OfRun(runEnd + 1))
                runEnd++;
            int Index(int[] notes, int pitch) => Array.IndexOf(notes, Nearest(notes, pitch));
            var at = Index(scale, note.Pitch);
            var direction = OfRun(k - 1) ? Math.Sign(note.Pitch - planned[k - 1].Pitch) : 0;
            var moves = ChordNotes(note.Position).Select(x => Index(scale, x) - at).Where(x => x != 0 && Math.Abs(x) <= LandingSteps).Distinct()
                .OrderBy(Math.Abs).ThenBy(x => Math.Sign(x) == direction ? 0 : 1);
            foreach (var move in moves)
            foreach (var (first, last) in new[] { (k, runEnd), (k, cellEnd), (cellStart, cellEnd) })
            {
                var moved = Enumerable.Range(first, last - first + 1).Select(i => ScaleNotes(planned[i].Position) is var notes && Index(notes, planned[i].Pitch) + move is var index
                    && index >= 0 && index < notes.Length ? notes[index] : (int?)null).ToArray();
                if (moved.Any(x => x is null))
                    continue;
                int PitchAt(int i) => i >= first && i <= last ? moved[i - first]!.Value : planned[i].Pitch;
                if (Enumerable.Range(Math.Max(1, first), Math.Min(planned.Count - 1, last + 1) - Math.Max(1, first) + 1).Any(i => PitchAt(i) == PitchAt(i - 1) && planned[i].Pitch != planned[i - 1].Pitch))
                    continue;

                for (var i = first; i <= last; i++)
                    planned[i] = planned[i] with { Pitch = PitchAt(i) };
                StateTrace.RecordSection(TracePoints.SoloLanding, SectionGenerator.SectionTrace, span.SectionId, $"{note.Position}: {move} steps", (note.Position, move));
                return;
            }
        }

        for (var k = 0; k < planned.Count; k++)
            if (planned[k].Run && chordChanges.Any(x => Math.Abs(x - planned[k].Position) < 1e-9) && Lands(k))
                Land(k);

        // a run's note off the beat into its next on the beat a whole step away played the semitone beside that note now and
        // then, a chromatic approach into it, as a jazz line puts its chord's notes on the beats, by its own key's draw
        // on a pulse of its bar, the meter's beats, a dotted quarter's in 6/8 and an odd meter's groups, the bars counted from
        // the section's start
        bool OnBeat(double position) => span.Meter.Pulses.Any(x => Math.Abs((position - barOrigin).Mod(span.Meter.BarDuration) - x) < 1e-6);
        bool Chromatic(int k) => Draws(planned[k].Position, ChromaticStream, soloist.ChromaticChance);
        // the notes as they sound, each kept as its scale step over the chord it plays under, a new chord's at the beat
        int Sounding(int k)
        {
            var chord = Chord(MelodyAt(planned[k].Position).Value, planned[k].Position);
            return chord.GetPitch(LinePlacement.GetScaleStep(chord, planned[k].Pitch));
        }

        for (var k = 0; k + 1 < planned.Count; k++)
            if (planned[k].Run && planned[k + 1].Run && planned[k].Phrase == planned[k + 1].Phrase && !OnBeat(planned[k].Position) && OnBeat(planned[k + 1].Position)
                && Math.Abs(Sounding(k + 1) - Sounding(k)) == 2 && Chromatic(k))
                planned[k] = planned[k] with { Pitch = Sounding(k + 1) - Math.Sign(Sounding(k + 1) - Sounding(k)), Altered = true };

        int FoldTowards(int pitch, int towards) => pitch + 12 * (int)Math.Round((towards - pitch) / 12.0);
        // each phrase louder at its highest note and softer at its lowest, as a line's (LinePattern.PhraseArc); and a stacked
        // note off the beat that leads into a chord change planed into it, an approach, where the soloist approaches
        var line = planned.Select(Place).ToArray();
        var arc = LinePattern.PhraseArc([..planned.Select(x => x.Phrase)], [..planned.Select(x => x.Pitch)], soloist.PhraseArc, 0);
        int ChangeAt(double position) => common.GetEffectiveStateMapAt(position).GetStateValue(StateKinds.ChordChange);
        // off a pulse of its bar, the bars counted from the section's start, as an answer's gap may start on a half beat
        bool OffThePulse(double position) => !span.Meter.Pulses.Any(x => Math.Abs((position - barOrigin).Mod(span.Meter.BarDuration) - x) < 1e-6);
        bool Approaches(int i) => soloist.Approaches && i + 1 < line.Length && line[i].Value.GetStateValue(CompositionStateKinds.LineVoices) > 1
                                  && OffThePulse(line[i].Position) && ChangeAt(line[i + 1].Position) != ChangeAt(line[i].Position);
        return ([
            ..line.Select((x, i) =>
            {
                var state = x.Value.With(StateKinds.Velocity, x.Value.GetStateValue(StateKinds.Velocity) + arc[i]);
                return (Approaches(i) ? state.With(CompositionStateKinds.LinePlanes, 1) : state).ToTimelineItem(x.Position);
            })
        ], plan);
    }

    /// <summary>
    ///     A note of a solo's line as planned, before it is placed: where it starts, its pitch, how it is played, and whether
    ///     its pitch is off the scale, a chromatic approach's or a tune's blue note, and how much louder it plays than its
    ///     intensity has it, a velocity sum.
    /// </summary>
    private sealed record PlannedNote(double Position, int Pitch, double Intensity, double Held, bool Accented, bool Release, bool Stacked, bool Octave, bool Ghosts, bool Run, int Phrase, bool Altered, double Lift);

    /// <summary>Where a run is in its cells: on which cell, on which note of it, from which note the cell starts and which way it goes.</summary>
    internal sealed class CellCursor(int root)
    {
        public int Cell { get; set; }
        public int Note { get; set; }
        public int Root { get; set; } = root;

        /// <summary>The way it goes, up or down, none before its first cell, which takes the way to where it heads.</summary>
        public int Direction { get; set; }
    }

    /// <summary>
    ///     A solo's segment as planned: where it starts and ends, its intensity, its gesture, the speed it runs at in notes a
    ///     beat, its cells' shape and sizes, whether it goes on with the run of the segment before, the phrase it is of, and
    ///     whether it is the phrase's release.
    /// </summary>
    internal sealed record Segment(double Start, double End, double Intensity, SoloGesture Gesture, int Speed, SoloShape Shape, int[] Sizes, bool GoesOn, int Phrase, bool IsRelease);

    /// <summary>
    ///     A solo's segments, a pitched one's or a drum solo's, phrase by phrase: each phrase of one of the soloist's
    ///     lengths, a wild soloist's now and then coming in a beat or two late; its peak following the solo's arc to the
    ///     climax and easing after, moved about by the soloist's volatility; its intensity shaped over it by a contour; its
    ///     gestures, each of a length of its own, the longer the more intense, so that a run goes on over the bar lines in it,
    ///     its gesture, speed, shape and cells from the soloist's vocabulary, leaned by the intensity there; and its release, a
    ///     note held from the last strong beat a beat or more before its end, and a breath after it now and then.
    /// </summary>
    /// <param name="quoteWeight">How much a quote weighs from a segment's start to its end: what there is to quote there.</param>
    /// <param name="beatsASecond">The tempo at a position, in beats a second, which the speeds keep under the soloist's fastest.</param>
    /// <param name="turn">How long a soloist's turn is where several trade, which no phrase runs over; the span's length where none do.</param>
    /// <param name="barOrigin">Where the bars the span plays in start, its section's start, which its strong beats are counted from, as an answer's span starts off the bar.</param>
    internal static Segment[] Plan(IGenerationContext context, IGenerationContext pickups, SectionSpan span, Soloist soloist, Func<double, double, double> quoteWeight, Func<double, double> beatsASecond,
        double turn, double barOrigin)
    {
        var bar = span.Meter.BarDuration;
        // a bar's strong beats, its groups' starts, as an odd meter's 3+2 are, 3/4's three beats and 6/8's two, so that a
        // one-bar phrase in a short bar releases on a beat after its first
        double[] strong = [..span.Meter.Levels[1].Select(x => x.Start / 4.0)];
        // the last strong beat of the bars from a position up to another; none where there is none
        double LastStrong(double from, double limit)
        {
            for (var b = Math.Floor((limit - barOrigin) / bar + 1e-9); b >= Math.Floor((from - barOrigin) / bar + 1e-9); b--)
                if (strong.Select(x => barOrigin + b * bar + x).Where(x => x >= from - 1e-9 && x <= limit + 1e-9).DefaultIfEmpty(double.NaN).Max() is var at && !double.IsNaN(at))
                    return at;
            return double.NaN;
        }
        var segments = new List<Segment>();
        // how long the run going on has played at each speed, which the soloist's stamina keeps it to
        var played = new Dictionary<int, double>();
        for (var (from, phrase) = (span.Start, 0); from < span.End - 1e-9; phrase++)
        {
            // the phrase, to its turn's end at the latest, and to the turn's or the solo's end rather than leave a scrap of
            // less than a bar
            var bars = context.Pick(soloist.PhraseBars.Select(x => new Weighted<int>(1, x)).ToImmutableArray());
            var turnEnd = Math.Min(span.End, span.Start + (Math.Floor((from - span.Start) / turn + 1e-9) + 1) * turn);
            // a spacious soloist's phrases by its section's lines instead: a call of a bar opening a line, an answer of a bar or
            // two after it, and the rest of the line a rest, the band's, where the line has the bars for all three
            var lineBars = span.Meter.PhraseBars % SpaceLineBars == 0 ? SpaceLineBars : span.Meter.PhraseBars;
            var lineStart = barOrigin + Math.Floor((from - barOrigin) / (lineBars * bar) + 1e-9) * lineBars * bar;
            var lineEnd = Math.Min(turnEnd, lineStart + lineBars * bar);
            // and only a line that starts in the span, not an answer's gap in the middle of one, and where a phrase before
            // ran into the line, only with a bar's answer and a bar's rest left of it
            var spacious = soloist.Spacious && lineStart >= span.Start - 1e-9 && lineEnd - lineStart >= (SpaceCallBars + 2) * bar - 1e-9
                           && (Math.Abs(from - lineStart) < 1e-9 || lineEnd - from >= 2 * bar - 1e-9);
            if (spacious)
                bars = Math.Abs(from - lineStart) < 1e-9 ? SpaceCallBars : Math.Min(bars, (int)Math.Round((lineEnd - from) / bar) - 1);
            var to = Math.Min(turnEnd, from + bars * bar);
            if (turnEnd - to < bar - 1e-9 && !spacious)
                to = turnEnd;
            var rests = spacious && Math.Abs(from - lineStart) > 1e-9;

            // its peak, on the way to the solo's climax or easing after it, and the shape of its intensity; in a solo of
            // choruses, each building in turn under the whole's arc, as a player drops back and builds again
            var place = ((from + to) / 2 - span.Start) / span.Duration;
            var pattern = span.Meter.PatternDuration;
            var arc = span.Meter.PatternBarCount >= WaveBars && span.Duration >= 2 * pattern - 1e-9
                ? Arc(((from + to) / 2 - span.Start) % pattern / pattern) * (WaveFloor + (1 - WaveFloor) * Arc(place))
                : Arc(place);
            var peak = Math.Clamp(arc + soloist.Volatility * Drift * Generators.SplineValue()(context), 0.05, 1);
            var contour = context.Pick(Contours);
            // a wild soloist's coming in a beat or two late, where the phrase has a bar to spare
            var isLate = context.TestProbability(soloist.OffBarChance);
            var late = context.GenerateInt(1, 3);
            var start = isLate && to - from >= 2 * bar ? from + late : from;
            // or one after the first coming in early, on a pickup out of the rest the phrase before leaves or the end of its
            // release, which it cuts short, half a beat of the rest left at least and a beat of the release, never at a turn's
            // start nor over a note; drawn for every phrase
            var (picks, lead) = (pickups.TestProbability(soloist.PickupChance), PickupLeads[pickups.GenerateInt(0, PickupLeads.Length)]);
            var atTurn = Math.Abs((from - span.Start) / turn - Math.Round((from - span.Start) / turn)) < 1e-9;
            if (picks && phrase > 0 && start == from && !atTurn && segments.Count > 0 && segments[^1] is var before && Math.Abs(before.End - from) < 1e-9
                && before.End - before.Start >= lead + (before.Gesture == SoloGesture.Pause ? PickupRoom : 1) - 1e-9 && (before.Gesture == SoloGesture.Pause || before.IsRelease))
            {
                segments[^1] = before with { End = from - lead };
                var pace = soloist.Speeds.Where(x => x <= PickupSpeed).DefaultIfEmpty(soloist.Speeds[0]).Max();
                segments.Add(new Segment(from - lead, from, peak * Contour(contour, 0), SoloGesture.Run, pace, soloist.Shapes[0], soloist.Groups ? [..soloist.CellSizes] : [soloist.CellSizes[0]], false, phrase, false));
                // the run it starts, which the phrase's first run goes on from, kept to the soloist's stamina with it
                played.Clear();
                played[pace] = lead;
            }

            // and its release from the last strong beat a beat or more before its end, or, where it has none, as an answer in
            // a short gap, from its last half beat half a beat or more before its end; and whether a breath follows
            var releaseAt = Math.Max(start, LastStrong(from, to - 1) is var last && !double.IsNaN(last)
                ? last
                : barOrigin + Math.Floor(2 * (to - 0.5 - barOrigin) + 1e-9) / 2);
            var breathes = context.TestProbability(soloist.BreathChance);
            if (start > from)
                segments.Add(new Segment(from, start, peak * Contour(contour, 0), SoloGesture.Pause, soloist.Speeds[0], soloist.Shapes[0], [soloist.CellSizes[0]], false, phrase, false));

            for (var at = start; at < releaseAt - 1e-9;)
            {
                // the intensity there, as the contour has it, moved about, or thrown somewhere else
                var target = peak * Contour(contour, (at - from) / (to - from));
                var jumps = context.TestProbability(soloist.Volatility * JumpRate);
                var thrown = context.GenerateDouble();
                var drift = Generators.SplineValue()(context);
                var level = Math.Clamp(jumps ? thrown : target + soloist.Volatility * Drift * drift, 0, 1);
                var lean = Tilt.Of(IntensityOdds, 2 * level - 1);
                // no longer than a bar, a note held that long being a bar's at most
                var length = Math.Min(Math.Min(context.Pick(lean.Weigh(GestureLengths.Select(x => new Weighted<double>(1, x.Beats)), x => GestureLengths.Single(g => g.Beats.Equals(x)).Lean)), bar), releaseAt - at);
                var end = at + length;
                var previous = segments.Count > 0 && segments[^1].Phrase == phrase ? segments[^1] : null;

                // the gesture
                var weights = Gestures.Select(x => new Weighted<SoloGesture>(
                    x.Gesture switch
                    {
                        SoloGesture.Quote => quoteWeight(at, end) * soloist.GestureWeights[x.Gesture],
                        SoloGesture.Rhythm => soloist.Riffs ? soloist.GestureWeights[x.Gesture] : 0,
                        // a pause is a breath between gestures: never a phrase's first, nor one after another
                        SoloGesture.Pause => previous is null or { Gesture: SoloGesture.Pause } ? 0 : soloist.GestureWeights[x.Gesture] * soloist.PauseRate / Soloist.PauseRates.Tuned,
                        _ => soloist.GestureWeights[x.Gesture]
                    },
                    x.Gesture));
                // a spacious soloist's the likelier held, the less likely running
                var gesture = context.Pick((soloist.Spacious ? new Tilt(lean.LogOdds + Tilt.Of(Soloist.SpaceOdds, -1).LogOdds) : lean)
                    .Weigh(weights.Where(x => x.Weight > 0), x => Gestures.Single(g => g.Gesture == x).Lean));
                // and its runs short, a burst into the next note
                if (soloist.Spacious && gesture == SoloGesture.Run && length > Soloist.SpaceRun + 1e-9)
                    (length, end) = (Soloist.SpaceRun, at + Soloist.SpaceRun);
                // a run after a run in the phrase goes on, its shape and its cells as they were
                var goingOn = gesture == SoloGesture.Run && previous is { Gesture: SoloGesture.Run };
                if (!goingOn)
                    played.Clear();
                // the speed, faster the more intense and a run's the faster, of those the soloist can play at the tempo there and keep up,
                // the fastest a burst of the most intense alone
                var speeds = soloist.Speeds
                    .Where(x => x * beatsASecond(at) <= soloist.MaxNotesASecond + 1e-9 && played.GetValueOrDefault(x) + length <= soloist.BeatsAt(x) + 1e-9
                                && (x <= BurstSpeed || level >= BurstIntensity - 1e-9))
                    .DefaultIfEmpty(soloist.Speeds[0])
                    .ToArray();
                var speedLean = gesture == SoloGesture.Run ? new Tilt(lean.LogOdds + Tilt.Of(RunSpeedOdds, 1).LogOdds) : lean;
                var speed = context.Pick(speedLean.Weigh(speeds.Select(x => new Weighted<int>(1, x)), x => speeds.Length == 1 ? 0 : 2.0 * Array.IndexOf(speeds, x) / (speeds.Length - 1) - 1));
                // and the cells: one of the soloist's shapes, and its sizes in turn where it groups them, or one of them
                var shape = context.Pick(soloist.Shapes.Select(x => new Weighted<SoloShape>(1, x)).ToImmutableArray());
                var size = context.Pick(soloist.CellSizes.Select(x => new Weighted<int>(1, x)).ToImmutableArray());
                int[] sizes = soloist.Groups ? [..soloist.CellSizes] : [size];
                if (goingOn)
                    (shape, sizes) = (previous!.Shape, previous.Sizes);
                if (gesture == SoloGesture.Run)
                    played[speed] = played.GetValueOrDefault(speed) + length;
                segments.Add(new Segment(at, end, level, gesture, speed, shape, sizes, goingOn, phrase, false));
                at = end;
            }

            // the release, held to the phrase's end, or to halfway there and a breath
            var releaseLevel = peak * Contour(contour, 1);
            var releaseEnd = Math.Min(releaseAt + bar, breathes && to - releaseAt >= 2 ? releaseAt + (to - releaseAt) / 2 : to);
            segments.Add(new Segment(releaseAt, releaseEnd, releaseLevel, SoloGesture.Held, soloist.Speeds[0], soloist.Shapes[0], [soloist.CellSizes[0]], false, phrase, true));
            if (releaseEnd < to - 1e-9)
                segments.Add(new Segment(releaseEnd, to, releaseLevel, SoloGesture.Pause, soloist.Speeds[0], soloist.Shapes[0], [soloist.CellSizes[0]], false, phrase, false));
            // and an answer's line rested to its end, the band's
            if (rests && lineEnd > to + 1e-9)
            {
                segments.Add(new Segment(to, lineEnd, releaseLevel, SoloGesture.Pause, soloist.Speeds[0], soloist.Shapes[0], [soloist.CellSizes[0]], false, phrase, false));
                to = lineEnd;
            }

            from = to;
        }

        return [..segments];
    }

    /// <summary>A run's or a lick's notes in a segment, a lick's at half the speed, as it plays its cell again in place.</summary>
    internal static double[] Positions(double start, double end, SoloGesture gesture, int speed)
    {
        var notesABeat = gesture == SoloGesture.Lick ? Math.Max(2, speed / 2) : speed;
        return [..Enumerable.Range(0, (int)Math.Round((end - start) * notesABeat)).Select(i => start + i / (double)notesABeat).Where(x => x < end - 1e-9)];
    }

    /// <summary>
    ///     A run's or a lick's pitches, as many as asked, cell by cell from where the cursor is: each cell the shape's figure
    ///     from its first note, as many notes as its size, its sizes taken in turn; a run's cells each starting on where the
    ///     shape moves the last, the way to where it heads, turning back at the range's end, and a lick's all on its note.
    ///     The first note of every cell is marked, to be played the louder. Answers the sizes of the cells it started.
    /// </summary>
    internal static (int[] Pitches, bool[] Accents, int[] Cells) Cells(IReadOnlyList<int> scale, CellCursor cursor, int heading, int count, SoloShape shape, int[] sizes, bool moves)
    {
        if (count == 0 || scale.Count == 0)
            return ([], [], []);

        int Index(int pitch) => Enumerable.Range(0, scale.Count).MinBy(i => Math.Abs(scale[i] - pitch));
        var (figure, advance, continues) = Shape(shape);
        var root = Index(cursor.Root);
        if (cursor.Direction == 0)
            cursor.Direction = Index(heading) >= root ? 1 : -1;
        var (pitches, accents, cells) = (new List<int>(), new List<bool>(), new List<int>());
        while (pitches.Count < count)
        {
            var size = sizes[cursor.Cell % sizes.Length];
            // a cell that would leave the range turns the run round
            int Reach(int i) => root + cursor.Direction * figure[i % figure.Length];
            if (cursor.Note == 0)
            {
                cells.Add(size);
                if (Enumerable.Range(0, size).Any(i => Reach(i) < 0 || Reach(i) >= scale.Count))
                    cursor.Direction = -cursor.Direction;
                // and a cell played in place, an arpeggio's or a lick's, that leaves it either way moves its root in where the
                // range has room for it, rather than repeat the edge's note; a run steps on from where it was
                var steps = Enumerable.Range(0, size).Select(i => cursor.Direction * figure[i % figure.Length]).ToArray();
                if (!continues && steps.Max() - steps.Min() < scale.Count)
                    root = Math.Clamp(root, -steps.Min(), scale.Count - 1 - steps.Max());
            }

            for (; cursor.Note < size && pitches.Count < count; cursor.Note++)
            {
                pitches.Add(scale[Math.Clamp(Reach(cursor.Note), 0, scale.Count - 1)]);
                accents.Add(cursor.Note == 0);
            }

            if (cursor.Note < size)
                break;

            // the next cell on, a scale's from where this one ended, or in place, a lick's
            if (moves)
                root = Math.Clamp(continues ? Reach(size - 1) + cursor.Direction : root + cursor.Direction * advance, 0, scale.Count - 1);
            (cursor.Cell, cursor.Note) = (cursor.Cell + 1, 0);
        }

        cursor.Root = scale[root];
        return ([..pitches], [..accents], [..cells]);
    }
}

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
    Quote
}

/// <summary>
///     A solo's line, phrase by phrase and gesture by gesture (<see cref="Plan" />): phrases of the soloist's lengths, their
///     peaks building to the solo's climax and easing after, each ending on a release, a note of the chord held from a
///     strong beat; within a phrase gestures of lengths of their own, picked by its intensity there, a run the likelier the
///     more intense, a held note or a pause the less, from the soloist's vocabulary (<see cref="Soloist" />), a run going
///     on over the bar lines in the phrase. Each gesture heads for where the
///     next starts: a note of the chord there, higher the more intense, in a solo of its own, or the melody's note there in
///     one on the melody, which quotes it the more. Every note is a scale note in the soloist's range, as its chord there
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
        (SoloGesture.Pause, new ByConvention(1, 0.4, 0.3), -1)
    ];

    /// <summary>How much a quote weighs in a solo of its own and in one on the melody, which paraphrases it.</summary>
    public const double OwnQuoteWeight = 0.15;

    public const double MelodyQuoteWeight = 2;

    /// <summary>The odds the most intense gesture has over the calmest at an intensity of 1, and the other way at 0.</summary>
    public const double IntensityOdds = 16;

    /// <summary>How much a run leans to the soloist's faster speeds beyond the intensity, as the odds of 1, a run being a soloist's fastest playing.</summary>
    public const double RunSpeedOdds = 3;

    /// <summary>
    ///     The solo's arc, which its phrases' peaks follow: where it starts, the climax it rises to at about 70% of the way,
    ///     and where it eases to by its end.
    /// </summary>
    public const double ArcStart = 0.3;

    public const double ClimaxAt = 0.7;

    public const double ArcEnd = 0.6;

    /// <summary>How far the soloist's volatility moves a phrase's peak and a gesture's intensity about.</summary>
    public const double Drift = 0.4;

    /// <summary>The chance a gesture's intensity is thrown anywhere, times the soloist's volatility.</summary>
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
    ///     turn's a step, an arpeggio's a third, a pedal's under its moving notes a step.
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
        [SoloGesture.Pause] = 0
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

    /// <summary>The steps above a chord's root its notes are: the root, the third and the fifth.</summary>
    private static readonly int[] ChordSteps = [0, 2, 4];

    /// <summary>A solo's line, and every segment's intensity and gesture, by its start.</summary>
    /// <param name="melody">The melody's notes in the solo, which a solo on the melody aims at and quotes, and every note takes its state from.</param>
    /// <param name="band">The chords' notes in the solo, the band's chord a stacked note stacks; the melody's where they rest.</param>
    /// <param name="track">The soloist's track, whose state its notes are realized over.</param>
    /// <param name="definition">The soloist's definition, its range.</param>
    /// <param name="voicing">The sequence that draws which gestures the soloist stacks, a stream of its own.</param>
    /// <param name="motifs">The sequence that draws which phrases open on the phrase before's motif, a stream of its own.</param>
    public static (TimelineItem<StateMap>[] Line, Segment[] Segments) Generate(
        IGenerationContext context,
        IGenerationContext voicing,
        IGenerationContext motifs,
        SectionSpan span,
        Soloist soloist,
        bool isOwn,
        IReadOnlyList<TimelineItem<StateMap>> melody,
        IReadOnlyList<TimelineItem<StateMap>> band,
        EventStateTimelineMap<StateMap> track,
        PitchInstrumentTrack definition,
        StateTimelineMap common
    )
    {
        var (low, high) = Realizer.GetRange(definition);
        // the soloist's register, its range in scale steps about the middle of the part's
        var middle = (low + high) / 2;
        var reach = (int)Math.Round(soloist.RegisterSteps * 12 / 14.0);
        var (bottom, top) = (Math.Max(low, middle - reach), Math.Min(high, middle + reach));

        // the chord a note plays over, as Realizer reads its state there
        ChordContext Chord(StateMap note, double position) => Realizer.GetChord(StateMap.Aggregate([
            note,
            track.GetEffectiveStateMapAt(position),
            definition.StateMap.OfScope(StateScope.Render),
            common.GetEffectiveStateMapAt(position)
        ])).Chord;

        // the melody's note sounding at a position, the first before it where none does, whose state a note there takes
        TimelineItem<StateMap> MelodyAt(double position) => melody.LastOrDefault(x => x.Position <= position + 1e-9, melody[0]);
        StateMap BandAt(double position) => band.Any(x => x.Position <= position + 1e-9) ? band.Last(x => x.Position <= position + 1e-9).Value : MelodyAt(position).Value;
        int MelodyPitch(TimelineItem<StateMap> note) =>
            Chord(note.Value, note.Position).GetPitch(note.Value.GetStateValue(StateKinds.ScaleStep)) + note.Value.GetStateValue(StateKinds.Alteration);
        int[] Notes(double position, IEnumerable<int> steps)
        {
            var chord = Chord(MelodyAt(position).Value, position);
            return [..steps.Select(chord.GetPitch).Where(x => x >= bottom && x <= top).Distinct().Order()];
        }

        int[] ScaleNotes(double position) => Notes(position, Enumerable.Range(-4 * Scales.StepCount, 12 * Scales.StepCount));
        int[] ChordNotes(double position) => Notes(position, Enumerable.Range(-4, 12).SelectMany(octave => ChordSteps.Select(x => x + octave * Scales.StepCount)));
        int Nearest(int[] notes, int pitch) => notes.Length == 0 ? Math.Clamp(pitch, bottom, top) : notes.MinBy(x => Math.Abs(x - pitch));

        // a solo as loud as the melody there, on average, rather than as a note of it it plays over, a soft pickup say, and
        // the louder the more intense
        var loudness = melody.Average(x => x.Value.GetStateValue(StateKinds.Velocity));

        // a note of the line at its place, as a line's note: the step above the chord there nearest it, on the scale there
        TimelineItem<StateMap> Place(double position, int pitch, double intensity, double held = 0, bool accented = false, bool release = false, bool stacked = false, bool octave = false)
        {
            var melodyNote = MelodyAt(position).Value;
            // in octaves as the soloist plays them, not as the melody it takes over did
            var state = melodyNote.Except([StateKinds.HeldDuration, CompositionStateKinds.LinePickup, StateKinds.ChordNotePitchOffsets, StateKinds.ChordVoicingFixed, CompositionStateKinds.Octave])
                .With(CompositionStateKinds.Octave, octave && soloist.Octaves ? 1 : 0);
            state = state.With(StateKinds.Velocity, loudness + IntensityVelocity * (intensity - 0.5) + (accented ? CellAccent : 0));
            state = state.With(StateKinds.ScaleStep, LinePlacement.GetScaleStep(Chord(state, position), pitch))
                .With(StateKinds.Alteration, 0)
                .With(CompositionStateKinds.SoloIntensity, (int)Math.Round(intensity * 100))
                .With(CompositionStateKinds.SoloRelease, release ? 1 : 0);
            // a stacked note the band's chord there under it, or the chord's shape planed (Realizer): the chords' note sounding
            // there, as the melody's before it may be of a chord before
            if (stacked && soloist.Voices > 1)
                state = state.With(StateKinds.ChordNotePitchOffsets, BandAt(position).GetStateValue(StateKinds.ChordNotePitchOffsets))
                    .With(CompositionStateKinds.LineVoices, soloist.Voices)
                    .With(CompositionStateKinds.LinePlanes, soloist.Planes ? 1 : 0);

            return (held > 0 ? state.With(StateKinds.HeldDuration, held) : state).ToTimelineItem(position);
        }

        var plan = Plan(
            context,
            span,
            soloist,
            // a quote wants the melody's notes there
            (start, end) => !melody.Any(n => n.Position >= start - 1e-9 && n.Position < end - 1e-9) ? 0 : isOwn ? OwnQuoteWeight : MelodyQuoteWeight,
            position => common.GetEffectiveStateMapAt(position).GetStateValue(StateKinds.Tempo) * Meter.BaseTempo / 60
        );
        var line = new List<TimelineItem<StateMap>>();
        var current = Nearest(ChordNotes(span.Start), middle);
        CellCursor? cursor = null;
        // the phrase before's motif, its first run's or lick's first cell, and the phrase whose motif is taken
        int[]? motif = null;
        var (motifPhrase, motifShape) = (-1, SoloShape.Scale);
        foreach (var (start, end, level, gesture, speed, shape, sizes, goesOn, phrase, isRelease) in plan)
        {
            // whether the soloist stacks the gesture, the likelier the more it leans to it and the more intense
            var stacks = voicing.TestProbability(new Tilt(Tilt.Of(ChordOdds, ChordLeans[gesture]).LogOdds + Tilt.Of(ChordOdds, 2 * level - 1).LogOdds).Chance(soloist.ChordRate, 1));
            // a run going on from the segment before keeps where it is in its cells
            cursor = goesOn && cursor is not null ? cursor : new CellCursor(current);
            // and where the gesture heads: a note of the chord there, higher the more intense, or the melody's there
            var heading = isOwn
                ? Nearest(ChordNotes(Math.Min(end, span.End - 1e-6)), (current + bottom + (int)Math.Round(level * (top - bottom))) / 2)
                : Nearest(ScaleNotes(end - 1e-6), FoldTowards(MelodyPitch(MelodyAt(Math.Min(end, span.End - 1e-6))), current));

            switch (gesture)
            {
                case SoloGesture.Held:
                    current = Nearest(ChordNotes(start), current);
                    line.Add(Place(start, current, level, end - start, release: isRelease, stacked: stacks, octave: true));
                    break;
                case SoloGesture.Run:
                case SoloGesture.Lick:
                {
                    // a lick at half the speed, its cell again in place, and a run its cells on the way to where it heads
                    var positions = Positions(start, end, gesture, speed);
                    var scale = ScaleNotes(start);
                    var (pitches, accents, cells) = Cells(scale, cursor, heading, positions.Length, shape, sizes, gesture == SoloGesture.Run);
                    // a phrase's first opening on the phrase before's motif a step away now and then, its own the next's
                    if (phrase != motifPhrase && pitches.Length > 0)
                    {
                        var answers = motifs.TestProbability(MotifChance);
                        var step = motifs.GenerateInt(0, 2) * 2 - 1;
                        // in the motif's own figure, a scale's answered by a scale
                        if (answers && motif is not null && shape == motifShape && scale.Length > 0)
                        {
                            // the motif in place of the first cell, and the rest moved with its last note, so that the
                            // gesture goes on from it as it went on from its own
                            int Index(int pitch) => Array.IndexOf(scale, Nearest(scale, pitch));
                            var count = Math.Min(motif.Length, pitches.Length);
                            var moved = Index(motif[count - 1]) + step - Index(pitches[count - 1]);
                            for (var i = 0; i < pitches.Length; i++)
                                pitches[i] = scale[Math.Clamp(i < count ? Index(motif[i]) + step : Index(pitches[i]) + moved, 0, scale.Length - 1)];
                        }
                        motif = pitches[..Math.Min(cells.Length > 0 ? cells[0] : pitches.Length, pitches.Length)];
                        (motifPhrase, motifShape) = (phrase, shape);
                    }

                    // a run, too fast to play in octaves, alone
                    line.AddRange(positions.Select((position, i) => Place(position, pitches[i], level, accented: accents[i], stacked: stacks && accents[i], octave: gesture == SoloGesture.Lick)));
                    StateTrace.Record(TracePoints.SoloCells, SectionGenerator.SectionTrace, span.SectionId, 0, StateMap.Default, 0, $"{gesture} {shape} {string.Join("+", cells)}", (start, shape, cells));
                    if (pitches.Length > 0)
                        current = pitches[^1];
                    break;
                }
                case SoloGesture.Quote:
                    foreach (var note in melody.Where(x => x.Position >= start - 1e-9 && x.Position < end - 1e-9))
                    {
                        current = Nearest(ScaleNotes(note.Position), FoldTowards(MelodyPitch(note), current));
                        line.Add(Place(note.Position, current, level, stacked: stacks, octave: true));
                    }

                    break;
            }
        }

        int FoldTowards(int pitch, int towards) => pitch + 12 * (int)Math.Round((towards - pitch) / 12.0);
        return ([..line], plan);
    }

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
    internal static Segment[] Plan(IGenerationContext context, SectionSpan span, Soloist soloist, Func<double, double, double> quoteWeight, Func<double, double> beatsASecond)
    {
        var bar = span.Meter.BarDuration;
        var strong = bar >= 4 ? bar / 2 : bar;
        var segments = new List<Segment>();
        // how long the run going on has played at each speed, which the soloist's stamina keeps it to
        var played = new Dictionary<int, double>();
        for (var (from, phrase) = (span.Start, 0); from < span.End - 1e-9; phrase++)
        {
            // the phrase, to the solo's end rather than leave a scrap of less than a bar
            var bars = context.Pick(soloist.PhraseBars.Select(x => new Weighted<int>(1, x)).ToImmutableArray());
            var to = Math.Min(span.End, from + bars * bar);
            if (span.End - to < bar - 1e-9)
                to = span.End;

            // its peak, on the way to the solo's climax or easing after it, and the shape of its intensity
            var place = ((from + to) / 2 - span.Start) / span.Duration;
            var arc = place < ClimaxAt ? ArcStart + (1 - ArcStart) * place / ClimaxAt : 1 - (1 - ArcEnd) * (place - ClimaxAt) / (1 - ClimaxAt);
            var peak = Math.Clamp(arc + soloist.Volatility * Drift * Generators.SplineValue()(context), 0.05, 1);
            var contour = context.Pick(Contours);
            // a wild soloist's coming in a beat or two late, where the phrase has a bar to spare
            var isLate = context.TestProbability(soloist.OffBarChance);
            var late = context.GenerateInt(1, 3);
            var start = isLate && to - from >= 2 * bar ? from + late : from;
            // and its release from the last strong beat a beat or more before its end, and whether a breath follows
            var releaseAt = Math.Max(start, from + Math.Floor((to - 1 - from) / strong + 1e-9) * strong);
            var breathes = context.TestProbability(soloist.BreathChance);
            if (start > from)
                segments.Add(new Segment(from, start, peak * Contour(contour, 0), SoloGesture.Pause, soloist.Speeds[0], soloist.Shapes[0], [soloist.CellSizes[0]], false, phrase, false));

            for (var at = start; at < releaseAt - 1e-9;)
            {
                // the intensity there, as the contour has it, moved about, or thrown somewhere else
                var target = peak * Contour(contour, (at - from) / (to - from));
                var jumps = context.TestProbability(Math.Min(1, soloist.Volatility * JumpRate));
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
                        SoloGesture.Quote => quoteWeight(at, end),
                        // a pause is a breath between gestures: never a phrase's first, nor one after another
                        SoloGesture.Pause => previous is null or { Gesture: SoloGesture.Pause } ? 0 : soloist.GestureWeights[x.Gesture] * soloist.PauseRate / Soloist.PauseRates.Tuned,
                        _ => soloist.GestureWeights[x.Gesture]
                    },
                    x.Gesture));
                var gesture = context.Pick(lean.Weigh(weights.Where(x => x.Weight > 0), x => Gestures.Single(g => g.Gesture == x).Lean));
                // a run after a run in the phrase goes on, its shape and its cells as they were
                var goingOn = gesture == SoloGesture.Run && previous is { Gesture: SoloGesture.Run };
                if (!goingOn)
                    played.Clear();
                // the speed, faster the more intense and a run's the faster, of those the soloist can play at the tempo there and keep up
                var speeds = soloist.Speeds
                    .Where(x => x * beatsASecond(at) <= soloist.MaxNotesASecond + 1e-9 && played.GetValueOrDefault(x) + length <= soloist.BeatsAt(x) + 1e-9)
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

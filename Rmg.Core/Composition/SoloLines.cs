using System.Collections.Immutable;
using Rmg.Core.Events;
using Rmg.Core.Probabilities;
using Rmg.Core.Songs;

namespace Rmg.Core.Composition;

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
///     A solo's line, gesture by gesture: the solo goes by half bars, or bars in a short meter, each with an intensity
///     that rises over the solo as a soloist builds, stepped about and now and then thrown somewhere else, as far and as
///     often as the soloist's volatility, and the gesture there picked by it, a run the likelier the more intense, a held
///     note or a pause the less, from the soloist's vocabulary (<see cref="Soloist" />). Each gesture heads for where the
///     next starts: a note of the chord there, higher the more intense, in a solo of its own, or the melody's note there in
///     one on the melody, which quotes it the more. Every note is a scale note in the soloist's range, as its chord there
///     has it, kept as its scale step over that chord, so that a run crossing a change of chord stays on the scale.
/// </summary>
internal static class SoloLines
{
    /// <summary>The gestures, their weights and how each leans with the intensity, from the calmest to the most intense.</summary>
    public static ImmutableArray<(SoloGesture Gesture, double Weight, double Lean)> Gestures { get; } =
    [
        (SoloGesture.Run, 1, 1),
        (SoloGesture.Lick, 0.8, 0.3),
        (SoloGesture.Quote, 1, 0),
        (SoloGesture.Held, 1, -0.6),
        (SoloGesture.Pause, 1, -1)
    ];

    /// <summary>How much a quote weighs in a solo of its own and in one on the melody, which paraphrases it.</summary>
    public const double OwnQuoteWeight = 0.15;

    public const double MelodyQuoteWeight = 2;

    /// <summary>The odds the most intense gesture has over the calmest at an intensity of 1, and the other way at 0.</summary>
    public const double IntensityOdds = 16;

    /// <summary>Where the intensity starts and how far it rises over the solo, the arc it is pulled towards.</summary>
    public const double ArcStart = 0.25;

    public const double ArcRise = 0.6;

    /// <summary>How far a step pulls the intensity towards the arc, and how far the soloist's volatility moves it about.</summary>
    public const double ArcPull = 0.5;

    public const double Drift = 0.4;

    /// <summary>The chance a step throws the intensity anywhere, times the soloist's volatility.</summary>
    public const double JumpRate = 0.5;

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

    /// <summary>How much louder a cell's first note is, so that its grouping is heard.</summary>
    public const double CellAccent = 0.1;

    /// <summary>The steps above a chord's root its notes are: the root, the third and the fifth.</summary>
    private static readonly int[] ChordSteps = [0, 2, 4];

    /// <summary>A solo's line, and every segment's intensity and gesture, by its start.</summary>
    /// <param name="melody">The melody's notes in the solo, which a solo on the melody aims at and quotes, and every note takes its state from.</param>
    /// <param name="track">The soloist's track, whose state its notes are realized over.</param>
    /// <param name="definition">The soloist's definition, its range.</param>
    public static (TimelineItem<StateMap>[] Line, (double Start, double Intensity, SoloGesture Gesture)[] Segments) Generate(
        IGenerationContext context,
        SectionSpan span,
        Soloist soloist,
        bool isOwn,
        IReadOnlyList<TimelineItem<StateMap>> melody,
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
        TimelineItem<StateMap> Place(double position, int pitch, double intensity, double held = 0, bool accented = false)
        {
            var state = MelodyAt(position).Value.Except([StateKinds.HeldDuration, CompositionStateKinds.LinePickup, StateKinds.ChordNotePitchOffsets, StateKinds.ChordVoicingFixed]);
            state = state.With(StateKinds.Velocity, loudness + IntensityVelocity * (intensity - 0.5) + (accented ? CellAccent : 0));
            state = state.With(StateKinds.ScaleStep, LinePlacement.GetScaleStep(Chord(state, position), pitch))
                .With(StateKinds.Alteration, 0)
                .With(CompositionStateKinds.SoloIntensity, (int)Math.Round(intensity * 100));
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
        foreach (var (start, end, level, gesture, speed, shape, sizes, goesOn) in plan)
        {
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
                    line.Add(Place(start, current, level, end - start));
                    break;
                case SoloGesture.Run:
                case SoloGesture.Lick:
                {
                    // a lick at half the speed, its cell again in place, and a run its cells on the way to where it heads
                    var positions = Positions(start, end, gesture, speed);
                    var (pitches, accents, cells) = Cells(ScaleNotes(start), cursor, heading, positions.Length, shape, sizes, gesture == SoloGesture.Run);
                    line.AddRange(positions.Select((position, i) => Place(position, pitches[i], level, accented: accents[i])));
                    StateTrace.Record(TracePoints.SoloCells, SectionGenerator.SectionTrace, span.SectionId, 0, StateMap.Default, 0, $"{gesture} {shape} {string.Join("+", cells)}", (start, shape, cells));
                    if (pitches.Length > 0)
                        current = pitches[^1];
                    break;
                }
                case SoloGesture.Quote:
                    foreach (var note in melody.Where(x => x.Position >= start - 1e-9 && x.Position < end - 1e-9))
                    {
                        current = Nearest(ScaleNotes(note.Position), FoldTowards(MelodyPitch(note), current));
                        line.Add(Place(note.Position, current, level));
                    }

                    break;
            }
        }

        int FoldTowards(int pitch, int towards) => pitch + 12 * (int)Math.Round((towards - pitch) / 12.0);
        var segments = plan.Select(x => (x.Start, x.Intensity, x.Gesture)).ToArray();
        return ([..line], segments);
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
    ///     beat, its cells' shape and sizes, and whether it goes on with the run of the segment before.
    /// </summary>
    internal sealed record Segment(double Start, double End, double Intensity, SoloGesture Gesture, int Speed, SoloShape Shape, int[] Sizes, bool GoesOn);

    /// <summary>
    ///     A solo's segments, a pitched one's or a drum solo's, by half bars, or bars in a short meter: each segment's intensity,
    ///     pulled towards an arc rising over the solo and moved about, or thrown somewhere else, as far and as often as the
    ///     soloist's volatility, and the gesture, the speed, the shape and the cells there, from its vocabulary, leaned by it.
    /// </summary>
    /// <param name="quoteWeight">How much a quote weighs from a segment's start to its end: what there is to quote there.</param>
    /// <param name="beatsASecond">The tempo at a position, in beats a second, which the speeds keep under the soloist's fastest.</param>
    internal static Segment[] Plan(IGenerationContext context, SectionSpan span, Soloist soloist, Func<double, double, double> quoteWeight, Func<double, double> beatsASecond)
    {
        var segment = span.Meter.BarDuration >= 4 ? span.Meter.BarDuration / 2 : span.Meter.BarDuration;
        var count = Math.Max(1, (int)Math.Round(span.Duration / segment));
        var segments = new List<Segment>();
        var level = ArcStart;
        for (var k = 0; k < count; k++)
        {
            var (start, end) = (span.Start + k * segment, Math.Min(span.End, span.Start + (k + 1) * segment));

            // the intensity, pulled towards the arc and moved about, or thrown somewhere else
            var arc = ArcStart + ArcRise * (count == 1 ? 1 : k / (double)(count - 1));
            var jumps = context.TestProbability(Math.Min(1, soloist.Volatility * JumpRate));
            var thrown = context.GenerateDouble();
            var drift = Generators.SplineValue()(context);
            level = Math.Clamp(jumps ? thrown : level + (arc - level) * ArcPull + soloist.Volatility * Drift * drift, 0, 1);
            var lean = Tilt.Of(IntensityOdds, 2 * level - 1);

            // the gesture
            var weights = Gestures.Select(x => new Weighted<SoloGesture>(
                x.Gesture switch
                {
                    SoloGesture.Quote => quoteWeight(start, end),
                    // a pause is a breath between gestures: never the first, nor one after another
                    SoloGesture.Pause => k == 0 || segments[^1].Gesture == SoloGesture.Pause ? 0 : x.Weight * soloist.PauseRate / Soloist.PauseRates.Tuned,
                    _ => x.Weight
                },
                x.Gesture));
            var gesture = context.Pick(lean.Weigh(weights.Where(x => x.Weight > 0), x => Gestures.Single(g => g.Gesture == x).Lean));
            // the speed, faster the more intense, of those the soloist can play at the tempo there
            var speeds = soloist.Speeds.Where(x => x * beatsASecond(start) <= soloist.MaxNotesASecond + 1e-9).DefaultIfEmpty(soloist.Speeds[0]).ToArray();
            var speed = context.Pick(lean.Weigh(speeds.Select(x => new Weighted<int>(1, x)), x => speeds.Length == 1 ? 0 : 2.0 * Array.IndexOf(speeds, x) / (speeds.Length - 1) - 1));
            // and the cells: one of the soloist's shapes, and its sizes in turn where it groups them, or one of them
            var shape = context.Pick(soloist.Shapes.Select(x => new Weighted<SoloShape>(1, x)).ToImmutableArray());
            var size = context.Pick(soloist.CellSizes.Select(x => new Weighted<int>(1, x)).ToImmutableArray());
            int[] sizes = soloist.Groups ? [..soloist.CellSizes] : [size];
            // a run after a run goes on, its shape and its cells as they were
            var goesOn = gesture == SoloGesture.Run && segments.Count > 0 && segments[^1].Gesture == SoloGesture.Run;
            if (goesOn)
                (shape, sizes) = (segments[^1].Shape, segments[^1].Sizes);
            segments.Add(new Segment(start, end, level, gesture, speed, shape, sizes, goesOn));
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

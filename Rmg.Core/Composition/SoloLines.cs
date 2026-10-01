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

        // a note of the line at its place, as a line's note: the step above the chord there nearest it, on the scale there
        TimelineItem<StateMap> Place(double position, int pitch, double intensity, double held = 0)
        {
            var state = MelodyAt(position).Value.Except([StateKinds.HeldDuration, CompositionStateKinds.LinePickup, StateKinds.ChordNotePitchOffsets, StateKinds.ChordVoicingFixed]);
            state = state.With(StateKinds.ScaleStep, LinePlacement.GetScaleStep(Chord(state, position), pitch))
                .With(StateKinds.Alteration, 0)
                .With(CompositionStateKinds.SoloIntensity, (int)Math.Round(intensity * 100));
            return (held > 0 ? state.With(StateKinds.HeldDuration, held) : state).ToTimelineItem(position);
        }

        // the segments, by half bars where a bar has four beats or more
        var segment = span.Meter.BarDuration >= 4 ? span.Meter.BarDuration / 2 : span.Meter.BarDuration;
        var count = Math.Max(1, (int)Math.Round(span.Duration / segment));
        var line = new List<TimelineItem<StateMap>>();
        var segments = new List<(double, double, SoloGesture)>();
        var level = ArcStart;
        var current = Nearest(ChordNotes(span.Start), middle);
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

            // the gesture, and where it heads: a note of the chord there, higher the more intense, or the melody's there
            var weights = Gestures.Select(x => new Weighted<SoloGesture>(
                x.Gesture switch
                {
                    // a quote wants the melody's notes there
                    SoloGesture.Quote => !melody.Any(n => n.Position >= start - 1e-9 && n.Position < end - 1e-9) ? 0 : isOwn ? OwnQuoteWeight : MelodyQuoteWeight,
                    // a pause is a breath between gestures: never the first, nor one after another
                    SoloGesture.Pause => k == 0 || segments[^1].Item3 == SoloGesture.Pause ? 0 : x.Weight * soloist.PauseRate / Soloist.PauseRates.Tuned,
                    _ => x.Weight
                },
                x.Gesture));
            var gesture = context.Pick(lean.Weigh(weights.Where(x => x.Weight > 0), x => Gestures.Single(g => g.Gesture == x).Lean));
            var speed = context.Pick(lean.Weigh(soloist.Speeds.Select(x => new Weighted<int>(1, x)), x => soloist.Speeds.Length == 1 ? 0 : 2.0 * soloist.Speeds.IndexOf(x) / (soloist.Speeds.Length - 1) - 1));
            var heading = isOwn
                ? Nearest(ChordNotes(Math.Min(end, span.End - 1e-6)), (current + bottom + (int)Math.Round(level * (top - bottom))) / 2)
                : Nearest(ScaleNotes(end - 1e-6), FoldTowards(MelodyPitch(MelodyAt(Math.Min(end, span.End - 1e-6))), current));
            segments.Add((start, level, gesture));

            switch (gesture)
            {
                case SoloGesture.Held:
                    current = Nearest(ChordNotes(start), current);
                    line.Add(Place(start, current, level, end - start));
                    break;
                case SoloGesture.Run:
                case SoloGesture.Lick:
                {
                    // a lick at half the speed, about its note, and a run the scale's way to where it heads
                    var notesABeat = gesture == SoloGesture.Lick ? Math.Max(2, speed / 2) : speed;
                    var positions = Enumerable.Range(0, (int)Math.Round((end - start) * notesABeat)).Select(i => start + i / (double)notesABeat).Where(x => x < end - 1e-9).ToArray();
                    var scale = ScaleNotes(start);
                    var pitches = gesture == SoloGesture.Run ? Run(scale, current, heading, positions.Length) : Lick(scale, current, positions.Length);
                    line.AddRange(positions.Zip(pitches, (position, pitch) => Place(position, pitch, level)));
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

            int FoldTowards(int pitch, int towards) => pitch + 12 * (int)Math.Round((towards - pitch) / 12.0);
        }

        return ([..line], [..segments]);
    }

    /// <summary>
    ///     A run's pitches, as many as asked, by the scale from the note after the start: the way to where it heads, or as
    ///     much of it as the run has notes for, and where it has notes to spare, away first and back, turning at the
    ///     range's ends.
    /// </summary>
    internal static int[] Run(IReadOnlyList<int> scale, int start, int heading, int count)
    {
        if (count == 0 || scale.Count == 0)
            return [];

        int Index(int pitch) => Enumerable.Range(0, scale.Count).MinBy(i => Math.Abs(scale[i] - pitch));
        var (from, to) = (Index(start), Index(heading));
        var direction = to >= from ? 1 : -1;
        // the notes to spare go away from where it heads first, half of them, and come back
        var spare = Math.Max(0, count - Math.Abs(to - from));
        var path = new List<int>();
        var at = from;
        var away = -direction;
        for (var i = 0; i < count; i++)
        {
            var step = i < spare / 2 ? away : i < spare ? -away : direction;
            if (at + step < 0 || at + step >= scale.Count)
                step = -step;
            at = Math.Clamp(at + step, 0, scale.Count - 1);
            path.Add(scale[at]);
        }

        return [..path];
    }

    /// <summary>A lick's pitches: its note, a step above, the note and a step below, again, as many as asked.</summary>
    internal static int[] Lick(IReadOnlyList<int> scale, int start, int count)
    {
        if (count == 0 || scale.Count == 0)
            return [];

        var at = Enumerable.Range(0, scale.Count).MinBy(i => Math.Abs(scale[i] - start));
        int[] figure = [0, 1, 0, -1];
        return [..Enumerable.Range(0, count).Select(i => scale[Math.Clamp(at + figure[i % figure.Length], 0, scale.Count - 1)])];
    }
}

using System.Collections.Immutable;
using Rmg.Core.Events;
using Rmg.Core.Probabilities;
using Rmg.Core.Rendering;
using Rmg.Core.Songs;

namespace Rmg.Core.Composition;

/// <summary>How several soloists share a solo.</summary>
public enum SoloMode
{
    /// <summary>They take its phrases in turn.</summary>
    Trading,

    /// <summary>They play it together, the others a similar line a step or more away, some of its notes left out.</summary>
    Together
}

/// <summary>A section's appearance played as a solo: who solos, and how several share it.</summary>
/// <param name="Soloists">The parts that solo; the drums alone for a drum solo.</param>
/// <param name="Mode">How several share it.</param>
/// <param name="Step">How many scale steps from the first the others play, playing together.</param>
/// <param name="IsOwn">Whether its line is its own, over the chords, rather than on the melody's.</param>
public sealed record SoloPlan(ImmutableArray<TrackRole> Soloists, SoloMode Mode, int Step, bool IsOwn)
{
    public bool IsDrumSolo => Soloists.Contains(TrackRole.Drum);
}

/// <summary>
///     Solos: a section's later appearance played as a solo now and then, by the form facet: none in the plainest songs,
///     and at the wild end as likely as not, so that a wild song may be solos throughout without having to be. A pitched
///     solo is a line of the solo's own (<see cref="SoloLines" />), on the melody's near the plain end and its own over
///     the chords at the wild, by the melody facet, played by the soloists instead of the melody, the melody resting; several soloists take its phrases in turn or play it together, the others a similar line; the drums solo
///     alone, the band resting, a fill at every bar. Who solos leans by the melody facet: near the plain end the solo
///     part, a lead of its own, the likeliest, and the melody's instrument, the chords or the riff now and then, and at
///     the wild end any part as likely.
/// </summary>
internal static class Solos
{
    /// <summary>The chance a later appearance is a solo, by the form facet.</summary>
    public static ByConvention Chance { get; } = new(0, 0.06, 0.5);

    /// <summary>
    ///     How a section's role leans it to a solo: a bridge the most, played as a solo in its place, a verse's changes
    ///     after it, a chorus less, which is sung, and a pre-chorus the least, too short to solo over; a section with no
    ///     role not at all.
    /// </summary>
    public static ImmutableDictionary<SectionRole, double> RoleLeans { get; } = new Dictionary<SectionRole, double>
    {
        [SectionRole.Free] = 0,
        [SectionRole.Verse] = 0.5,
        [SectionRole.PreChorus] = -1,
        [SectionRole.Chorus] = -0.5,
        [SectionRole.Bridge] = 1
    }.ToImmutableDictionary();

    /// <summary>The odds a role leaning to a solo has, as the odds of 1.</summary>
    public const double RoleOdds = 3;

    /// <summary>How many parts solo at once.</summary>
    public static ImmutableArray<(int Count, ByConvention Weight)> Counts { get; } =
    [
        (1, new ByConvention(1, 0.85, 1)),
        (2, new ByConvention(0, 0.12, 1)),
        (3, new ByConvention(0, 0.03, 1))
    ];

    /// <summary>Who solos, and how likely each is.</summary>
    public static ImmutableArray<(TrackRole Part, ByConvention Weight)> Soloists { get; } =
    [
        (TrackRole.Solo, new ByConvention(4, 0.45, 1)),
        (TrackRole.Melody, new ByConvention(1, 0.2, 1)),
        (TrackRole.Chords, new ByConvention(1, 0.13, 1)),
        (TrackRole.Riff, new ByConvention(1, 0.1, 1)),
        (TrackRole.Rhythm, new ByConvention(1, 0.07, 1)),
        (TrackRole.CounterMelody, new ByConvention(0, 0.08, 1)),
        (TrackRole.Bass, new ByConvention(0, 0.05, 1)),
        (TrackRole.Pad, new ByConvention(0, 0.02, 1)),
        (TrackRole.Drum, new ByConvention(0, 0.05, 1))
    ];

    public static ImmutableArray<(SoloMode Mode, ByConvention Weight)> Modes { get; } =
    [
        (SoloMode.Trading, new ByConvention(1, 0.6, 1)),
        (SoloMode.Together, new ByConvention(1, 0.4, 1))
    ];

    /// <summary>The chance a solo's line is its own rather than on the melody's, by the melody facet: never at the plain end, always at the wild.</summary>
    public static ByConvention OwnChance { get; } = new(0, 0.5, 1);

    /// <summary>The steps from the first soloist the others play together: the same notes, a third or a sixth away.</summary>
    public static ImmutableArray<int> Steps { get; } = [0, 2, -2, 5, -5];

    /// <summary>The chance a note of a soloist playing together with the first is left out, which tells their lines apart.</summary>
    public const double LeftOutChance = 0.2;

    /// <summary>How much louder a soloist plays than the loudest part under it, on the average of their velocities as rendered.</summary>
    public const double Margin = 0.06;

    /// <summary>An appearance's solo, drawn whatever it turns out to be, so that the draws stay as they are; none for no solo.</summary>
    /// <param name="index">The appearance's place among the song's sections; the first is never a solo.</param>
    /// <param name="role">The section's role in the song's form, which leans it to a solo.</param>
    /// <param name="parts">The parts the song has.</param>
    public static SoloPlan? Draw(IGenerationContext context, int index, SectionRole role, Unconventionality facets, IReadOnlyCollection<TrackRole> parts)
    {
        var isSolo = context.TestProbability(Tilt.Of(RoleOdds, RoleLeans[role]).Chance(Chance.At(facets[Facet.Form]), 1));
        var melody = facets[Facet.Melody];
        var count = context.Pick(ByConvention.Weigh(Counts, melody));
        var mode = context.Pick(ByConvention.Weigh(Modes, melody));
        var step = Steps[context.GenerateInt(0, Steps.Length)];
        var candidates = Soloists.Where(x => parts.Contains(x.Part)).ToList();
        var soloists = new List<TrackRole>();
        for (var i = 0; i < count && candidates.Count > 0 && candidates.Any(x => x.Weight.At(melody) > 0); i++)
        {
            var soloist = context.Pick(ByConvention.Weigh(candidates, melody));
            soloists.Add(soloist);
            candidates.RemoveAll(x => x.Part == soloist);
        }

        var isOwn = context.TestProbability(OwnChance.At(melody));

        if (!isSolo || index == 0 || soloists.Count == 0 || !parts.Contains(TrackRole.Melody) && !soloists.Contains(TrackRole.Drum))
            return null;

        // the drums solo alone
        return soloists.Contains(TrackRole.Drum) ? new SoloPlan([TrackRole.Drum], SoloMode.Trading, 0, false) : new SoloPlan([..soloists], mode, step, isOwn);
    }

    /// <summary>
    ///     The song with every pitched solo's line (<see cref="SoloLines" />) played by its soloists instead of the melody:
    ///     in turn by phrase, or together, the others a step away with some of its notes left out, each note a line's,
    ///     whatever its part plays otherwise, and every soloist a margin over the loudest part playing under it.
    /// </summary>
    /// <param name="definitions">Every track's definition, the soloists' ranges.</param>
    /// <param name="soloists">Every part's soloist, whose vocabulary its solos are played in.</param>
    /// <param name="solos">Every solo, by its appearance's place among the song's sections.</param>
    /// <param name="streams">A solo's sequence, by its appearance's place.</param>
    /// <param name="voicings">A solo's sequence of the gestures its soloist stacks, by its appearance's place.</param>
    public static TrackEventStateTimelineMap<StateMap> Apply(
        TrackEventStateTimelineMap<StateMap> song,
        IReadOnlyDictionary<int, TrackRole> roles,
        IReadOnlyDictionary<int, IInstrumentTrack> definitions,
        SongMap map,
        IReadOnlyDictionary<TrackRole, Soloist> soloists,
        IReadOnlyDictionary<int, SoloPlan> solos,
        Func<int, IGenerationContext> streams,
        Func<int, IGenerationContext> voicings
    )
    {
        var trackOf = roles.GroupBy(x => x.Value).ToDictionary(x => x.Key, x => x.First().Key);
        foreach (var (index, solo) in solos.Where(x => !x.Value.IsDrumSolo).OrderBy(x => x.Key))
        {
            var span = map.Sections[index];
            var context = streams(index);
            bool IsIn(TimelineItem<StateMap> note) => note.Position >= span.Start - 1e-9 && note.Position < span.End - 1e-9;
            var melody = song.TrackTimelineMap[trackOf[TrackRole.Melody]].EventTimeline.Where(IsIn).ToArray();
            StateTrace.Record(TracePoints.SoloLine, trackOf[TrackRole.Melody], span.SectionId, 0, StateMap.Default, 0, $"{melody.Length} notes", (index, melody.Length));
            // a melody with no notes there leaves nothing to solo on, and the parts play as they would
            if (melody.Length == 0)
                continue;

            // a soloist with no notes of its own, such as the solo part, plays over the melody's state, whose line it takes over
            foreach (var soloist in solo.Soloists.Select(x => trackOf[x]).Where(x => !song.TrackTimelineMap.ContainsKey(x)))
                song = TrackEventStateTimelineMap.Create(song.Duration, song.TrackTimelineMap.SetItem(soloist, EventStateTimelineMap.Create(
                    song.Duration, EventTimeline.Create<StateMap>(song.Duration), song.TrackTimelineMap[trackOf[TrackRole.Melody]].StateTimelineMap)), song.CommonStateTimelineMap);

            // the solo's line, in the first soloist's vocabulary and range
            var first = trackOf[solo.Soloists[0]];
            var (line, segments) = SoloLines.Generate(context, voicings(index), span, soloists[solo.Soloists[0]], solo.IsOwn, melody, song.TrackTimelineMap[first], (PitchInstrumentTrack)definitions[first], song.CommonStateTimelineMap);
            StateTrace.Record(TracePoints.SoloGestures, first, span.SectionId, 0, StateMap.Default, 0, $"{(solo.IsOwn ? "own" : "on the melody")}, {segments.Length} segments", (index, solo.IsOwn, segments));
            var phrases = Math.Max(1, (int)Math.Round(span.Duration / span.Meter.PatternDuration));
            // a solo of one phrase is traded by its halves
            var turn = phrases > 1 ? span.Meter.PatternDuration : span.Meter.PatternDuration / 2;

            // as loud as the band's loudest part there and a margin over it, the melody resting and the drums apart
            var band = song.TrackTimelineMap
                .Where(x => definitions[x.Key] is PitchInstrumentTrack && x.Key != trackOf[TrackRole.Melody] && !solo.Soloists.Any(s => trackOf[s] == x.Key))
                .Select(x => Loudness(Realizer.GetNoteStates(x.Value, definitions[x.Key], song.CommonStateTimelineMap).Where(IsIn), 0))
                .Where(x => !double.IsNaN(x))
                .DefaultIfEmpty(0)
                .Max();
            var moves = new Dictionary<int, Func<EventTimeline<StateMap>, EventTimeline<StateMap>>>();
            foreach (var (soloist, place) in solo.Soloists.Select((x, i) => (x, i)))
            {
                TimelineItem<StateMap>[] notes = solo.Mode == SoloMode.Trading
                    ? [..line.Where(x => (int)Math.Floor((x.Position - span.Start) / turn + 1e-9) % solo.Soloists.Length == place)]
                    : place == 0
                        ? line
                        : [..line.Where(_ => !context.TestProbability(LeftOutChance))
                            .Select(x => x.Value.With(StateKinds.ScaleStep, x.Value.GetStateValue(StateKinds.ScaleStep) + solo.Step).With(StateKinds.Alteration, 0).ToTimelineItem(x.Position))];
                // over the band, however loud its part and its draws there, as a pad that solos is not comping any more
                var track = song.TrackTimelineMap[trackOf[soloist]];
                var heard = Realizer.GetNoteStates(
                    EventStateTimelineMap.Create(song.Duration, EventTimeline.Create(song.Duration, notes), track.StateTimelineMap),
                    definitions[trackOf[soloist]],
                    song.CommonStateTimelineMap
                ).ToArray();
                var lift = Lift(heard, band + Margin);
                TimelineItem<StateMap>[] played =
                [
                    // a soloist that plays no chords playing the line's stacked notes alone
                    ..notes.Select(x => x.Value.With(CompositionStateKinds.LineSolo, 1)
                        .With(CompositionStateKinds.LineVoices, soloists[soloist].ChordRate > 0 ? x.Value.GetStateValue(CompositionStateKinds.LineVoices) : 0)
                        .With(StateKinds.Velocity, x.Value.GetStateValue(StateKinds.Velocity) + lift)
                        .ToTimelineItem(x.Position))
                ];
                moves[trackOf[soloist]] = own => EventTimeline.Create(own.Duration, own.Where(x => !IsIn(x)).Concat(played).OrderBy(x => x.Position));
            }

            // the melody rests where it is not a soloist
            if (!solo.Soloists.Contains(TrackRole.Melody))
                moves[trackOf[TrackRole.Melody]] = own => EventTimeline.Create(own.Duration, own.Where(x => !IsIn(x)));
            song = song.MapTrackEvents(moves);
        }

        return song;
    }

    /// <summary>The average velocity of notes as rendered, each moved by a lift; none where there are no notes.</summary>
    internal static double Loudness(IEnumerable<TimelineItem<StateMap>> notes, double lift) =>
        notes.Select(x => Render.ToMidiVelocity(x.Value.GetStateValue(StateKinds.Velocity) + lift)).DefaultIfEmpty(double.NaN).Average();

    /// <summary>
    ///     How much the notes' velocities move so that their average as rendered is the loudness aimed at, or as near the
    ///     loudest as a velocity is where it cannot be.
    /// </summary>
    internal static double Lift(IReadOnlyList<TimelineItem<StateMap>> notes, double loudness)
    {
        if (notes.Count == 0)
            return 0;

        // the lift that renders the loudness aimed at, between a lift that renders less and one past any it can need
        var (low, high) = (-LiftReach, LiftReach);
        for (var i = 0; i < LiftSteps; i++)
        {
            var middle = (low + high) / 2;
            if (Loudness(notes, middle) < loudness)
                low = middle;
            else
                high = middle;
        }

        return high;
    }

    private const double LiftReach = 4;

    private const int LiftSteps = 30;
}

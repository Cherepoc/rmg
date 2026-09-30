using System.Collections.Immutable;
using Rmg.Core.Events;
using Rmg.Core.Probabilities;
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
public sealed record SoloPlan(ImmutableArray<TrackRole> Soloists, SoloMode Mode, int Step)
{
    public bool IsDrumSolo => Soloists.Contains(TrackRole.Drum);
}

/// <summary>
///     Solos: a section's later appearance played as a solo now and then, by the form facet: none in the plainest songs,
///     and at the wild end as likely as not, so that a wild song may be solos throughout without having to be. A pitched
///     solo is the melody's line for that appearance improvised through, played by the soloists instead, the melody
///     resting; several soloists take its phrases in turn or play it together, the others a similar line; the drums solo
///     alone, the band resting, a fill at every bar. Who solos leans by the melody facet: near the plain end the melody's
///     instrument, the chords or the riff, and at the wild end any part as likely.
/// </summary>
internal static class Solos
{
    /// <summary>The chance a later appearance is a solo, by the form facet.</summary>
    public static ByConvention Chance { get; } = new(0, 0.06, 0.5);

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
        (TrackRole.Melody, new ByConvention(1, 0.35, 1)),
        (TrackRole.Chords, new ByConvention(1, 0.25, 1)),
        (TrackRole.Riff, new ByConvention(1, 0.2, 1)),
        (TrackRole.Rhythm, new ByConvention(1, 0.15, 1)),
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

    /// <summary>The steps from the first soloist the others play together: the same notes, a third or a sixth away.</summary>
    public static ImmutableArray<int> Steps { get; } = [0, 2, -2, 5, -5];

    /// <summary>The chance a note of a soloist playing together with the first is left out, which tells their lines apart.</summary>
    public const double LeftOutChance = 0.2;

    /// <summary>An appearance's solo, drawn whatever it turns out to be, so that the draws stay as they are; none for no solo.</summary>
    /// <param name="index">The appearance's place among the song's sections; the first is never a solo.</param>
    /// <param name="parts">The parts the song has.</param>
    public static SoloPlan? Draw(IGenerationContext context, int index, Unconventionality facets, IReadOnlyCollection<TrackRole> parts)
    {
        var isSolo = context.TestProbability(Chance.At(facets[Facet.Form]));
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

        if (!isSolo || index == 0 || soloists.Count == 0 || !parts.Contains(TrackRole.Melody) && !soloists.Contains(TrackRole.Drum))
            return null;

        // the drums solo alone
        return soloists.Contains(TrackRole.Drum) ? new SoloPlan([TrackRole.Drum], SoloMode.Trading, 0) : new SoloPlan([..soloists], mode, step);
    }

    /// <summary>
    ///     The song with every pitched solo's line moved from the melody to its soloists: in turn by phrase, or together,
    ///     the others a step away with some of its notes left out, each note a line's, whatever its part plays otherwise.
    /// </summary>
    /// <param name="solos">Every solo, by its appearance's place among the song's sections.</param>
    /// <param name="streams">A solo's sequence, by its appearance's place.</param>
    public static TrackEventStateTimelineMap<StateMap> Apply(
        TrackEventStateTimelineMap<StateMap> song,
        IReadOnlyDictionary<int, TrackRole> roles,
        SongMap map,
        IReadOnlyDictionary<int, SoloPlan> solos,
        Func<int, IGenerationContext> streams
    )
    {
        var trackOf = roles.GroupBy(x => x.Value).ToDictionary(x => x.Key, x => x.First().Key);
        foreach (var (index, solo) in solos.Where(x => !x.Value.IsDrumSolo).OrderBy(x => x.Key))
        {
            var span = map.Sections[index];
            var context = streams(index);
            bool IsIn(TimelineItem<StateMap> note) => note.Position >= span.Start - 1e-9 && note.Position < span.End - 1e-9;
            var line = song.TrackTimelineMap[trackOf[TrackRole.Melody]].EventTimeline.Where(IsIn).ToArray();
            // a melody with no notes there leaves nothing to solo on, and the parts play as they would
            if (line.Length == 0)
                continue;
            var phrases = Math.Max(1, (int)Math.Round(span.Duration / map.Meter.PatternDuration));
            // a solo of one phrase is traded by its halves
            var turn = phrases > 1 ? map.Meter.PatternDuration : map.Meter.PatternDuration / 2;

            var moves = new Dictionary<int, Func<EventTimeline<StateMap>, EventTimeline<StateMap>>>();
            foreach (var (soloist, place) in solo.Soloists.Select((x, i) => (x, i)))
            {
                var notes = solo.Mode == SoloMode.Trading
                    ? line.Where(x => (int)Math.Floor((x.Position - span.Start) / turn + 1e-9) % solo.Soloists.Length == place)
                    : place == 0
                        ? line
                        : line.Where(_ => !context.TestProbability(LeftOutChance))
                            .Select(x => x.Value.With(StateKinds.ScaleStep, x.Value.GetStateValue(StateKinds.ScaleStep) + solo.Step).With(StateKinds.Alteration, 0).ToTimelineItem(x.Position));
                TimelineItem<StateMap>[] played = [..notes.Select(x => x.Value.With(CompositionStateKinds.LineSolo, 1).ToTimelineItem(x.Position))];
                moves[trackOf[soloist]] = own => EventTimeline.Create(own.Duration, own.Where(x => !IsIn(x)).Concat(played).OrderBy(x => x.Position));
            }

            // the melody rests where it is not a soloist
            if (!solo.Soloists.Contains(TrackRole.Melody))
                moves[trackOf[TrackRole.Melody]] = own => EventTimeline.Create(own.Duration, own.Where(x => !IsIn(x)));
            song = song.MapTrackEvents(moves);
        }

        return song;
    }
}

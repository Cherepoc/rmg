using System.Collections.Immutable;
using Rmg.Core.Events;
using Rmg.Core.Probabilities;
using Rmg.Core.Songs;

namespace Rmg.Core.Composition;

/// <summary>
///     A section's polymeter: a figure of a length of its own, in 16ths, that divides no bar, played again and again across
///     the bar lines, as the guitar and the kick run against the hi-hat and the snare in Meshuggah, and meeting the bar
///     again where every 4-bar pattern starts it afresh. The riff plays it, or the bass where the song has no riff, and the
///     bass with the riff and the kick now and then; each part repeats its own first notes of the pattern, so that its
///     line, placed after, follows the chords where they land. By the feel facet of the section's unconventionality:
///     never at the plain end, rarely at the middle, and at the wild end in half the sections, of any length as likely.
/// </summary>
internal static class Polymeter
{
    public static ByConvention Chance { get; } = new(0, 0.01, 0.5);

    /// <summary>The figure's length in 16ths, three against four, a dotted 8th's, the most common, and any other wilder.</summary>
    public static ImmutableArray<(int Sixteenths, ByConvention Weight)> Lengths { get; } =
    [
        (3, new ByConvention(0, 0.3, 1)),
        (6, new ByConvention(0, 0.3, 1)),
        (5, new ByConvention(0, 0.1, 1)),
        (7, new ByConvention(0, 0.1, 1)),
        (9, new ByConvention(0, 0.04, 1)),
        (10, new ByConvention(0, 0.04, 1)),
        (11, new ByConvention(0, 0.03, 1)),
        (13, new ByConvention(0, 0.03, 1)),
        (15, new ByConvention(0, 0.03, 1)),
        (23, new ByConvention(0, 0.03, 1))
    ];

    /// <summary>The chance the bass, with a riff, and the kick play the figure with the part that leads it.</summary>
    public const double JoinChance = 0.5;

    private const double Sixteenth = 0.25;

    /// <summary>A section's polymeter: the figure's length and the parts that play it; none for a section in its bars.</summary>
    public sealed record Plan(int Sixteenths, ImmutableArray<TrackRole> Parts, bool Kick);

    /// <summary>Every section's polymeter, by its id, each drawing from a sequence of its own; none for one it does not play.</summary>
    /// <param name="sections">A section's draws, by its id.</param>
    /// <param name="feel">A section's feel facet, by its id.</param>
    /// <param name="meters">A section's meter, by its id, whose bar the figure's length must not divide.</param>
    /// <param name="absent">The parts the song leaves out.</param>
    public static ImmutableDictionary<int, Plan> Generate(
        Func<int, IGenerationContext> sections,
        Func<int, double> feel,
        IReadOnlyDictionary<int, Meter> meters,
        IEnumerable<int> sectionIds,
        ImmutableHashSet<TrackRole> absent
    )
    {
        var plans = ImmutableDictionary.CreateBuilder<int, Plan>();
        foreach (var id in sectionIds.Distinct())
        {
            var context = sections(id);
            var unconventionality = feel(id);
            if (!context.TestProbability(Chance.At(unconventionality)))
                continue;

            var bar = meters[id].Groups.Sum();
            var lengths = Lengths.Where(x => bar % x.Sixteenths != 0).ToArray();
            var length = context.Pick(ByConvention.Weigh(lengths, unconventionality));
            var hasRiff = !absent.Contains(TrackRole.Riff);
            var bassJoins = context.TestProbability(JoinChance);
            var kick = context.TestProbability(JoinChance) && !absent.Contains(TrackRole.Drum);
            ImmutableArray<TrackRole> parts = hasRiff ? bassJoins ? [TrackRole.Riff, TrackRole.Bass] : [TrackRole.Riff] : [TrackRole.Bass];
            plans[id] = new Plan(length, parts, kick);
        }

        return plans.ToImmutable();
    }

    /// <summary>
    ///     The song with every section's polymeter played: each part's notes of every pattern the first of its figure's
    ///     repeated from the pattern's start to its end, its own others there given up.
    /// </summary>
    public static TrackEventStateTimelineMap<StateMap> Apply(
        TrackEventStateTimelineMap<StateMap> song,
        IReadOnlyDictionary<int, TrackRole> roles,
        IEnumerable<int> kickTracks,
        SongMap map,
        ImmutableDictionary<int, Plan> plans
    )
    {
        var trackOf = roles.GroupBy(x => x.Value).ToDictionary(x => x.Key, x => x.First().Key);
        var kicks = kickTracks.ToArray();
        foreach (var span in map.Sections)
        {
            if (!plans.TryGetValue(span.SectionId, out var plan))
                continue;

            var tracks = plan.Parts.Where(trackOf.ContainsKey).Select(x => trackOf[x]).Concat(plan.Kick ? kicks : []).Where(song.TrackTimelineMap.ContainsKey);
            var figure = plan.Sixteenths * Sixteenth;
            var pattern = span.Meter.PatternDuration;
            song = song.MapTrackEvents(tracks.ToDictionary(x => x, _ => (Func<EventTimeline<StateMap>, EventTimeline<StateMap>>)(notes =>
            {
                var played = new List<TimelineItem<StateMap>>();
                for (var start = span.Start; start < span.End - 1e-9; start += pattern)
                {
                    var first = notes.Where(x => x.Position >= start - 1e-9 && x.Position < start + figure - 1e-9).ToArray();
                    for (var repeat = start; repeat < start + pattern - 1e-9; repeat += figure)
                        played.AddRange(first.Select(x => x.Value.ToTimelineItem(x.Position - start + repeat)).Where(x => x.Position < start + pattern - 1e-9));
                }

                return EventTimeline.Create(notes.Duration, notes.Where(x => x.Position < span.Start - 1e-9 || x.Position >= span.End - 1e-9).Concat(played).OrderBy(x => x.Position));
            })));
        }

        return song;
    }
}

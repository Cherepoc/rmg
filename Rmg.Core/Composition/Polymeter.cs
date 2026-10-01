using System.Collections.Immutable;
using Rmg.Core.Events;
using Rmg.Core.Probabilities;
using Rmg.Core.Songs;

namespace Rmg.Core.Composition;

/// <summary>
///     A section's polymeter: a cycle of a length of its own, in 16ths, that divides no bar, played again and again across
///     the bar lines, as a guitar and a kick run against the hi-hat and the snare, and meeting the bar
///     again where every 4-bar pattern, or every second at the wilder, starts it afresh. A cycle is a cell, or a cell
///     played again and then a closing one of another length, as 7, 7 and 9 make 23; each cell plays the same motif, cut
///     to its length. The riff plays it, or the bass where the song has no riff, the bass with the riff now and then, and
///     the kick with them, the oftener the wilder; each part repeats its own first notes of the cycle's start, so that its
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

    /// <summary>The chance the bass, with a riff, plays the cycle with it.</summary>
    public const double JoinChance = 0.5;

    /// <summary>The chance the kick plays the cycle with the part that leads it, as it would always in a band built on such cycles: the wilder, the oftener.</summary>
    public static ByConvention KickChance { get; } = new(0, 0.85, 1);

    /// <summary>The chance the cycle starts afresh every second pattern rather than every one, so that it drifts for 8 bars where a section has them.</summary>
    public static ByConvention FrameChance { get; } = new(0, 0.3, 0.6);

    /// <summary>The chance a cycle closes on a cell of another length, its first played once to three times before it.</summary>
    public static ByConvention TailChance { get; } = new(0, 0.3, 0.7);

    /// <summary>How many times a cycle with a closing cell plays its first before it.</summary>
    public static ImmutableArray<int> Repeats { get; } = [1, 2, 3];

    private const double Sixteenth = 0.25;

    /// <summary>
    ///     A section's polymeter: its cycle's cells' lengths in 16ths, in turn, the parts that play it, and how many patterns
    ///     it runs over before starting afresh; none for a section in its bars.
    /// </summary>
    public sealed record Plan(ImmutableArray<int> Cells, ImmutableArray<TrackRole> Parts, bool Kick, int Patterns)
    {
        /// <summary>The cycle's length in 16ths.</summary>
        public int Sixteenths => Cells.Sum();
    }

    /// <summary>Every section's polymeter, by its id, each drawing from a sequence of its own; none for one it does not play.</summary>
    /// <param name="sections">A section's draws, by its id.</param>
    /// <param name="feel">A section's feel facet, by its id.</param>
    /// <param name="meters">A section's meter, by its id, whose bar the cycle's length must not divide.</param>
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
            var kick = context.TestProbability(KickChance.At(unconventionality)) && !absent.Contains(TrackRole.Drum);
            ImmutableArray<TrackRole> parts = hasRiff ? bassJoins ? [TrackRole.Riff, TrackRole.Bass] : [TrackRole.Riff] : [TrackRole.Bass];
            // drawn after the rest, so that it stays as it was: the frame, and a closing cell of another length, the cycle as
            // a whole dividing no bar
            var frame = context.TestProbability(FrameChance.At(unconventionality)) ? 2 : 1;
            var tails = context.TestProbability(TailChance.At(unconventionality));
            var repeats = context.Pick(Repeats.Select(x => new Weighted<int>(1, x)).ToImmutableArray());
            var closing = Lengths.Where(x => x.Sixteenths != length && Drifts(length * repeats + x.Sixteenths, bar)).ToArray();
            var tail = closing.Length > 0 ? context.Pick(ByConvention.Weigh(closing, unconventionality)) : 0;
            ImmutableArray<int> cells = tails && tail > 0 ? [..Enumerable.Repeat(length, repeats), tail] : [length];
            plans[id] = new Plan(cells, parts, kick, frame);
        }

        return plans.ToImmutable();
    }

    /// <summary>Whether a cycle drifts against the bar: neither a whole number of bars nor dividing one.</summary>
    private static bool Drifts(int cycle, int bar) => bar % cycle != 0 && cycle % bar != 0;

    /// <summary>
    ///     The song with every section's polymeter played: each part's notes of every frame, a pattern or two, its first
    ///     cell's played cell by cell from the frame's start to its end, each cut to its cell's length, its own others there
    ///     given up, in the bars it plays; the drums bound to the kick, doubling or accenting it, with it.
    /// </summary>
    /// <param name="bindings">The drums bound to a lead in a section, by its id: their tracks and their leads'.</param>
    public static TrackEventStateTimelineMap<StateMap> Apply(
        TrackEventStateTimelineMap<StateMap> song,
        IReadOnlyDictionary<int, TrackRole> roles,
        IEnumerable<int> kickTracks,
        SongMap map,
        ImmutableDictionary<int, Plan> plans,
        Func<int, IReadOnlyDictionary<int, int>> bindings
    )
    {
        var trackOf = roles.GroupBy(x => x.Value).ToDictionary(x => x.Key, x => x.First().Key);
        var kicks = kickTracks.ToArray();
        foreach (var span in map.Sections)
        {
            if (!plans.TryGetValue(span.SectionId, out var plan))
                continue;

            var bound = bindings(span.SectionId).Where(x => kicks.Contains(x.Value)).Select(x => x.Key);
            var tracks = plan.Parts.Where(trackOf.ContainsKey).Select(x => trackOf[x]).Concat(plan.Kick ? kicks.Concat(bound) : []).Distinct().Where(song.TrackTimelineMap.ContainsKey);
            var longest = plan.Cells.Max() * Sixteenth;
            var frame = span.Meter.PatternDuration * plan.Patterns;
            song = song.MapTrackEvents(tracks.ToDictionary(x => x, _ => (Func<EventTimeline<StateMap>, EventTimeline<StateMap>>)(notes =>
            {
                // in the bars the part plays, none where it sits out
                int Bar(double position) => (int)Math.Floor((position - span.Start) / span.Meter.BarDuration + 1e-9);
                var plays = notes.Where(x => x.Position >= span.Start - 1e-9 && x.Position < span.End - 1e-9).Select(x => Bar(x.Position)).ToHashSet();
                var played = new List<TimelineItem<StateMap>>();
                for (var start = span.Start; start < span.End - 1e-9; start += frame)
                {
                    var end = Math.Min(start + frame, span.End);
                    var first = notes.Where(x => x.Position >= start - 1e-9 && x.Position < start + longest - 1e-9).ToArray();
                    var cell = 0;
                    for (var repeat = start; repeat < end - 1e-9; repeat += plan.Cells[cell++ % plan.Cells.Length] * Sixteenth)
                    {
                        var length = plan.Cells[cell % plan.Cells.Length] * Sixteenth;
                        played.AddRange(first.Where(x => x.Position - start < length - 1e-9).Select(x => x.Value.ToTimelineItem(x.Position - start + repeat)).Where(x => x.Position < end - 1e-9 && plays.Contains(Bar(x.Position))));
                    }
                }

                return EventTimeline.Create(notes.Duration, notes.Where(x => x.Position < span.Start - 1e-9 || x.Position >= span.End - 1e-9).Concat(played).OrderBy(x => x.Position));
            })));
        }

        return song;
    }
}

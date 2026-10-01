using System.Collections.Immutable;
using Rmg.Core.Events;
using Rmg.Core.Probabilities;
using Rmg.Core.Songs;

namespace Rmg.Core.Composition;

/// <summary>
///     A line doubled by another part in a section now and then, note for note, the other part's own notes there giving
///     way: the counter-melody a third or a sixth above or below the melody, as a second voice harmonises a tune, and the
///     bass with the riff, in its own register, as a band plays a riff together. A doubled note keeps its step above its
///     chord's root, moved by the interval, so that it stays in the scale over the same chords; a part plays it from its own
///     first note in the section to its last, and a section where either part rests is not doubled. A twin, the riff's,
///     plays the whole line wherever it plays, in every section, a third above or a sixth below, having no notes of its
///     own. Neither plainer nor stranger, a doubling does not lean by the song's unconventionality, but the twin's
///     interval, by the song's melody facet: a third or a sixth in a middling song, and the wilder the song, the oftener a
///     fourth, a fifth, a second or a seventh.
/// </summary>
internal static class LineDoubling
{
    /// <summary>
    ///     The steps a twin plays apart from the riff: a third above and a sixth below, the plainest, and a fourth above, a
    ///     fifth below, a second above and a seventh below, the wilder.
    /// </summary>
    public static ImmutableArray<(int Step, ByConvention Weight)> TwinSteps { get; } =
    [
        (2, new ByConvention(1, 0.55, 0.2)),
        (-5, new ByConvention(1, 0.35, 0.2)),
        (3, new ByConvention(0, 0.05, 0.2)),
        (-4, new ByConvention(0, 0.05, 0.2)),
        (1, new ByConvention(0, 0, 0.1)),
        (-6, new ByConvention(0, 0, 0.1))
    ];

    /// <summary>
    ///     Who doubles whom, by how many scale steps, weighed by the song's melody facet, the chance of a section, and whether
    ///     the target is the source's twin.
    /// </summary>
    public static ImmutableArray<(TrackRole Source, TrackRole Target, ImmutableArray<(int Step, ByConvention Weight)> Steps, double Chance, bool IsTwin)> Pairs { get; } =
    [
        (TrackRole.Melody, TrackRole.CounterMelody, [(2, Any), (-2, Any), (5, Any), (-5, Any)], 0.25, false),
        (TrackRole.Riff, TrackRole.Bass, [(0, Any)], 0.35, false),
        (TrackRole.Riff, TrackRole.RiffTwin, TwinSteps, 1, true)
    ];

    /// <summary>A step a doubling plays whatever the song's melody facet.</summary>
    private static ByConvention Any => new(1, 1, 1);

    /// <summary>The song with its lines doubled where each section draws a doubling, from a sequence of the pair's own for each section.</summary>
    /// <param name="streams">A pair's sequence for a section, by the pair's place and the section's.</param>
    /// <param name="melody">The song's melody facet, which the steps lean by.</param>
    public static TrackEventStateTimelineMap<StateMap> Apply(
        TrackEventStateTimelineMap<StateMap> song,
        IReadOnlyDictionary<int, TrackRole> roles,
        SongMap map,
        double melody,
        Func<int, int, IGenerationContext> streams
    )
    {
        var trackOf = roles.GroupBy(x => x.Value).ToDictionary(x => x.Key, x => x.First().Key);
        foreach (var ((source, target, steps, chance, isTwin), pair) in Pairs.Select((x, i) => (x, i)))
        {
            if (!trackOf.TryGetValue(source, out var sourceTrack) || !trackOf.TryGetValue(target, out var targetTrack))
                continue;

            // a twin plays over the source's own state, its chords and its bars', having none of its own
            if (isTwin)
            {
                var twin = EventStateTimelineMap.Create(song.Duration, EventTimeline.Create<StateMap>(song.Duration), song.TrackTimelineMap[sourceTrack].StateTimelineMap);
                song = TrackEventStateTimelineMap.Create(song.Duration, song.TrackTimelineMap.SetItem(targetTrack, twin), song.CommonStateTimelineMap);
            }

            foreach (var (span, index) in map.Sections.Select((x, i) => (x, i)))
            {
                var context = streams(pair, index);
                var isDoubled = context.TestProbability(chance);
                var step = context.Pick(ByConvention.Weigh(steps, melody));
                var events = song.TrackTimelineMap;
                bool Plays(int track) => events[track].EventTimeline.Any(x => x.Position >= span.Start - 1e-9 && x.Position < span.End - 1e-9);
                if (!isDoubled || !Plays(sourceTrack) || !isTwin && !Plays(targetTrack))
                    continue;

                StateTrace.Record(TracePoints.LineDoubling, targetTrack, span.SectionId, 0, StateMap.Default, 0, $"{source} {step}", (index, source, target, step));
                // where the doubling part plays in the section, from its first note to its last, so that it joins no earlier
                // than it would, as into an intro that brings it in
                var own = events[isTwin ? sourceTrack : targetTrack].EventTimeline.Where(x => x.Position >= span.Start - 1e-9 && x.Position < span.End - 1e-9).Select(x => x.Position).ToArray();
                var (from, to) = (own.Min(), own.Max());
                var doubled = events[sourceTrack].EventTimeline
                    .Where(x => x.Position >= from - 1e-9 && x.Position <= to + 1e-9)
                    .Select(x => x.Value.With(StateKinds.ScaleStep, x.Value.GetStateValue(StateKinds.ScaleStep) + step).With(StateKinds.Alteration, 0).ToTimelineItem(x.Position));
                song = song.MapTrackEvents(new Dictionary<int, Func<EventTimeline<StateMap>, EventTimeline<StateMap>>>
                {
                    [targetTrack] = notes => EventTimeline.Create(
                        notes.Duration,
                        notes.Where(x => x.Position < from - 1e-9 || x.Position > to + 1e-9).Concat(doubled).OrderBy(x => x.Position)
                    )
                });
            }
        }

        return song;
    }
}

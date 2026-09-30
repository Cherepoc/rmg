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
///     first note in the section to its last, and a section where either part rests is not doubled. Neither plainer nor stranger, a doubling
///     does not lean by the song's unconventionality.
/// </summary>
internal static class LineDoubling
{
    /// <summary>Who doubles whom, by how many scale steps, and the chance of a section.</summary>
    public static ImmutableArray<(TrackRole Source, TrackRole Target, ImmutableArray<int> Steps, double Chance)> Pairs { get; } =
    [
        (TrackRole.Melody, TrackRole.CounterMelody, [2, -2, 5, -5], 0.25),
        (TrackRole.Riff, TrackRole.Bass, [0], 0.35)
    ];

    /// <summary>The song with its lines doubled where each section draws a doubling, from a sequence of the pair's own for each section.</summary>
    /// <param name="streams">A pair's sequence for a section, by the pair's place and the section's.</param>
    public static TrackEventStateTimelineMap<StateMap> Apply(
        TrackEventStateTimelineMap<StateMap> song,
        IReadOnlyDictionary<int, TrackRole> roles,
        SongMap map,
        Func<int, int, IGenerationContext> streams
    )
    {
        var trackOf = roles.GroupBy(x => x.Value).ToDictionary(x => x.Key, x => x.First().Key);
        foreach (var ((source, target, steps, chance), pair) in Pairs.Select((x, i) => (x, i)))
        {
            if (!trackOf.TryGetValue(source, out var sourceTrack) || !trackOf.TryGetValue(target, out var targetTrack))
                continue;

            foreach (var (span, index) in map.Sections.Select((x, i) => (x, i)))
            {
                var context = streams(pair, index);
                var isDoubled = context.TestProbability(chance);
                var step = steps[context.GenerateInt(0, steps.Length)];
                var events = song.TrackTimelineMap;
                bool Plays(int track) => events[track].EventTimeline.Any(x => x.Position >= span.Start - 1e-9 && x.Position < span.End - 1e-9);
                if (!isDoubled || !Plays(sourceTrack) || !Plays(targetTrack))
                    continue;

                StateTrace.Record(TracePoints.LineDoubling, targetTrack, span.SectionId, 0, StateMap.Default, 0, $"{source} {step}", (index, source, target, step));
                // where the doubling part plays in the section, from its first note to its last, so that it joins no earlier
                // than it would, as into an intro that brings it in
                var own = events[targetTrack].EventTimeline.Where(x => x.Position >= span.Start - 1e-9 && x.Position < span.End - 1e-9).Select(x => x.Position).ToArray();
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

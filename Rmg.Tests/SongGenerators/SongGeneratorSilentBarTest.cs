using Rmg.Core.Events;
using Rmg.Core.Probabilities;

namespace Rmg.Tests.SongGenerators;

/// <summary>
///     A bar of a track pattern in which the rhythm drops every hit must still take its 4 beats. The song generator
///     builds a 4-bar pattern the same way as below: each bar is a note pattern turned into a
///     <see cref="TrackEventStateTimelineMap{T}" />, and the bars are put one after another with <c>Unroll</c>.
/// </summary>
public sealed class SongGeneratorSilentBarTest
{
    private const double BarDuration = 4;

    private static readonly StateMap BarStateMap = new StateMapBuilder()
        .Add(StateKinds.Velocity, 0.5)
        .ToStateMap(new GenerationContext(0));

    [Test]
    public async Task NotePattern_WithNoHits_KeepsItsDuration()
    {
        var context = new GenerationContext(0);
        // a weight this small counts as no chance at all, so every hit is dropped
        var rhythmPattern = DyadicRankThresholdPattern.Create(
            context,
            1,
            _ => 1e-4,
            new DyadicTimelineDescriptor(BarDuration, 1, 0, 2)
        );
        var notePattern = DyadicRankItemPattern<StateMap>.Create(
            context,
            rhythmPattern,
            _ => (_, _) => BarStateMap,
            1
        );

        await Assert.That(notePattern.GeneratedTimeline.Count == 0).IsTrue();
        await Assert.That(notePattern.GeneratedTimeline.Duration).IsEqualTo(BarDuration);
    }

    [Test]
    public async Task Unroll_WithSilentBar_KeepsLaterBarsInPlace()
    {
        var bars = new[]
        {
            CreateBar([new TimelineItem<StateMap>(0, BarStateMap)]),
            CreateBar([]),
            CreateBar([new TimelineItem<StateMap>(0, BarStateMap)])
        };

        var pattern = bars.Unroll();

        var notePositions = pattern.TrackTimelineMap[1].EventTimeline
            .AsEnumerable()
            .Select(x => x.Position)
            .ToArray();
        await Assert.That(notePositions).IsEquivalentTo([0, 2 * BarDuration]);
        await Assert.That(pattern.Duration).IsEqualTo(3 * BarDuration);
    }

    private static TrackEventStateTimelineMap<StateMap> CreateBar(TimelineItem<StateMap>[] notes)
    {
        // the same calls as SongGenerator's noteHigherPatternGenerator makes for a bar
        var noteTimeline = EventTimeline.Create(BarDuration, notes);
        var trackTimeline = noteTimeline.ToEventStateTimelineMap(BarStateMap);
        return TrackEventStateTimelineMap.Create(
            BarDuration,
            [new KeyValuePair<int, EventStateTimelineMap<StateMap>>(1, trackTimeline)],
            StateTimelineMap.Create(BarDuration)
        );
    }
}

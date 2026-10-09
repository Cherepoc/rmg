using Rmg.Core.Events;

namespace Rmg.Tests.EventTimelines;

/// <summary>Every item's length, to the next one's place, and the last's to the timeline's end.</summary>
public sealed class EventTimelineWithDurationsTest
{
    [Test]
    public async Task EveryItem_LastsToTheNext_AndTheLastToTheEnd()
    {
        var timeline = EventTimeline.Create(8, [new TimelineItem<int>(0, 1), new TimelineItem<int>(2, 2), new TimelineItem<int>(5, 3)]);

        var lasting = timeline.WithDurations().Select(x => (x.Position, x.Value.Value, x.Value.Duration)).ToArray();

        await Assert.That(lasting).IsEquivalentTo([(0.0, 1, 2.0), (2.0, 2, 3.0), (5.0, 3, 3.0)], CollectionOrdering.Matching);
    }

    [Test]
    public async Task ItemsAtOnePlace_LastNothing_ButTheLast()
    {
        var timeline = EventTimeline.Create(4, [new TimelineItem<int>(1, 1), new TimelineItem<int>(1, 2)]);

        var lasting = timeline.WithDurations().Select(x => x.Value.Duration).ToArray();

        await Assert.That(lasting).IsEquivalentTo([0.0, 3.0], CollectionOrdering.Matching);
    }

    [Test]
    public async Task AnEmptyTimeline_KeepsItsDuration()
    {
        var lasting = EventTimeline.Create<int>(4).WithDurations();

        await Assert.That(lasting.Count).IsEqualTo(0);
        await Assert.That(lasting.Duration).IsEqualTo(4);
    }
}

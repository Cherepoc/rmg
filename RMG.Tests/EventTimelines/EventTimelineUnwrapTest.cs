using Rmg.Core.Events;

namespace Rmg.Tests.EventTimelines;

public sealed class EventTimelineUnwrapTest
{
    [Test]
    [Arguments(0)]
    [Arguments(1)]
    [Arguments(2)]
    public async Task Empty_UnwrapsTo_Empty(int count)
    {
        var items = new TimelineItem<EventTimeline<int>>[count];
        for (var i = 0; i < count; i++)
            items[i] = new TimelineItem<EventTimeline<int>>(i, EventTimeline.Empty<int>());
        
        var input = EventTimeline.Create(count, items);
        
        var result = EventTimeline<int>.Unwrap(input);

        await Assert.That(result)
            .IsNotNull()
            .And.Satisfies(x => x.IsEmpty, assert => assert.IsTrue())
            .And.Satisfies(x => x.Duration, assert => assert.IsZero())
            .And.Satisfies(x => x.Count, assert => assert.IsZero())
            .And.Satisfies(x => x.AsEnumerable(), assert => assert.IsEmpty());
    }
    
    [Test]
    public async Task MultipleTimelines_UnwrapTo_SingleTimeline()
    {
        var items1 = new TimelineItem<int>[]
        {
            new(0, 1),
            new(1, 2),
        };
        var timeline1 = EventTimeline.Create(2, items1);
        
        var items2 = new TimelineItem<int>[]
        {
            new(2, 3),
            new(3, 4),
        };
        var timeline2 = EventTimeline.Create(4, items2);

        var timelineItems = new[]
        {
            timeline2.ToTimelineItem(1),
            timeline1.ToTimelineItem(0),
        };
        
        var input = EventTimeline.Create(4, timelineItems);
        
        var result = EventTimeline<int>.Unwrap(input);

        const double expectedDuration = 5;
        var expectedItems = new TimelineItem<int>[]
        {
            new(0, 1),
            new(1, 2),
            new(3, 3),
            new(4, 4),
        };

        await Assert.That(result)
            .IsNotNull()
            .And.Satisfies(x => x.IsEmpty, assert => assert.IsFalse())
            .And.Satisfies(x => x.Duration, assert => assert.IsEqualTo(expectedDuration))
            .And.Satisfies(x => x.Count, assert => assert.IsEqualTo(expectedItems.Length))
            .And.Satisfies(x => x.AsEnumerable(), assert => assert.IsEquivalentTo(expectedItems));
    }
}
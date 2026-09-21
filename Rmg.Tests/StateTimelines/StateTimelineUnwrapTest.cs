using Rmg.Core.Events;

namespace Rmg.Tests.StateTimelines;

public sealed class StateTimelineUnwrapTest
{
    private static readonly StateKind<int> StateKind = StateKinds.KeyOffset;
    
    [Test]
    [Arguments(0)]
    [Arguments(1)]
    [Arguments(2)]
    public async Task Empty_ResultsIn_Empty(int count)
    {
        var items = new TimelineItem<StateTimeline<int>>[count];
        for (var i = 0; i < count; i++)
            items[i] = new TimelineItem<StateTimeline<int>>(i, StateKind.EmptyTimeline);
        
        var input = EventTimeline.Create(count, items);
        
        var result = input.Unwrap();

        await Check.That(result)
            .IsNotNull()
            .And.Satisfies(x => x.IsEmpty, assert => assert.IsTrue())
            .And.Satisfies(x => x.Duration, assert => assert.IsZero())
            .And.Satisfies(x => x.Count, assert => assert.IsZero())
            .And.Satisfies(x => !(x.AsEnumerable()).Any(), assert => assert.IsTrue());
    }
    
    [Test]
    public async Task MultipleTimelines_ResultsIn_SingleTimeline()
    {
        var items1 = new TimelineItem<int>[]
        {
            new(0, 1),
            new(1, 2),
        };
        var timeline1 = StateTimeline.Create(2, StateKind, items1);
        
        var items2 = new TimelineItem<int>[]
        {
            new(2, 3),
            new(3, 4),
        };
        var timeline2 = StateTimeline.Create(4, StateKind, items2);
        
        var items3 = new TimelineItem<int>[]
        {
            new(0, 1),
            new(2, 5),
        };
        var timeline3 = StateTimeline.Create(3, StateKind, items3);

        var timelineItems = new[]
        {
            timeline1.ToTimelineItem(1),
            timeline2.ToTimelineItem(0),
            timeline3.ToTimelineItem(1.5),
            StateKind.EmptyTimeline.ToTimelineItem(1)
        };
        
        var input = EventTimeline.Create(4, timelineItems);
        
        var result = input.Unwrap();

        const double expectedDuration = 4.5;
        var expectedItems = new TimelineItem<int>[]
        {
            new(1, 1),
            new(1.5, 2),
            new(2, 6),
            new(3, 5),
            new(3.5, 9),
            new(4, 5),
        };

        await Check.That(result)
            .IsNotNull()
            .And.Satisfies(x => x.IsEmpty, assert => assert.IsFalse())
            .And.Satisfies(x => x.Duration, assert => assert.IsEqualTo(expectedDuration))
            .And.Satisfies(x => x.Count, assert => assert.IsEqualTo(expectedItems.Length))
            .And.Satisfies(x => x.AsEnumerable(), assert => assert.IsEquivalentTo(expectedItems));
    }
}
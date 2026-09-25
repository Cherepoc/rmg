using Rmg.Core.Events;

namespace Rmg.Tests.EventTimelines;

public sealed class EventTimelineMergeTest
{
    [Test]
    [Arguments(0)]
    [Arguments(1)]
    [Arguments(2)]
    public async Task ZeroDurationTimelines_MergeTo_ZeroDuration(int count)
    {
        var input = new EventTimeline<int>[count];
        for (var i = 0; i < count; i++)
            input[i] = EventTimeline.Create<int>(0);
        
        var result = EventTimeline.Merge(input);

        await Check.That(result)
            .IsNotNull()
            .And.Satisfies(x => x.Duration, assert => assert.IsZero())
            .And.Satisfies(x => x.Count, assert => assert.IsZero())
            .And.Satisfies(x => !(x.AsEnumerable()).Any(), assert => assert.IsTrue());
    }
    
    [Test]
    public async Task SingleTimeline_MergesTo_Itself()
    {
        const double duration = 2;
        var items = new TimelineItem<int>[]
        {
            new(0, 1),
            new(1, 2),
        };
        var timeline = EventTimeline.Create(duration, items);

        var input = new[]
        {
            timeline
        };
        
        var result = EventTimeline.Merge(input);

        await Check.That(result)
            .IsNotNull()
            .And.Satisfies(x => x.Duration, assert => assert.IsEqualTo(duration))
            .And.Satisfies(x => x.Count, assert => assert.IsEqualTo(items.Length))
            .And.Satisfies(x => x.AsEnumerable(), assert => assert.IsEquivalentTo(items));
    }
    
    [Test]
    public async Task MultipleTimelines_MergeTo_SingleTimeline()
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

        var input = new[]
        {
            timeline2,
            timeline1,
        };
        
        var result = EventTimeline.Merge(input);

        const double expectedDuration = 4;
        var expectedItems = new TimelineItem<int>[]
        {
            new(0, 1),
            new(1, 2),
            new(2, 3),
            new(3, 4),
        };

        await Check.That(result)
            .IsNotNull()
            .And.Satisfies(x => x.Duration, assert => assert.IsEqualTo(expectedDuration))
            .And.Satisfies(x => x.Count, assert => assert.IsEqualTo(expectedItems.Length))
            .And.Satisfies(x => x.AsEnumerable(), assert => assert.IsEquivalentTo(expectedItems));
    }
    
    [Test]
    public async Task MultipleSamePositionTimelines_MergeTo_SingleSamePositionTimeline()
    {
        var items1 = new TimelineItem<int>[]
        {
            new(0, 1),
            new(1, 2),
        };
        var timeline1 = EventTimeline.Create(2, items1);
        
        var items2 = new TimelineItem<int>[]
        {
            new(1, 3),
            new(2, 4),
        };
        var timeline2 = EventTimeline.Create(4, items2);

        var input = new[]
        {
            timeline2,
            timeline1,
        };
        
        var result = EventTimeline.Merge(input);

        const double expectedDuration = 4;
        var expectedItems = new TimelineItem<int>[]
        {
            new(0, 1),
            new(1, 3),
            new(1, 2),
            new(2, 4),
        };

        await Check.That(result)
            .IsNotNull()
            .And.Satisfies(x => x.Duration, assert => assert.IsEqualTo(expectedDuration))
            .And.Satisfies(x => x.Count, assert => assert.IsEqualTo(expectedItems.Length))
            .And.Satisfies(x => x.AsEnumerable(), assert => assert.IsEquivalentTo(expectedItems));
    }
}
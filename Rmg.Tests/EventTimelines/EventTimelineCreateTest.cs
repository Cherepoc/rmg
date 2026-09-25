using Rmg.Core.Events;

namespace Rmg.Tests.EventTimelines;

public sealed class EventTimelineCreateTest
{
    [Test]
    public async Task ZeroDuration_ResultsIn_ZeroDuration()
    {
        const double duration = 0;
        var items = new TimelineItem<int>[]
        {
            new(0, 1)
        };
        
        var result = EventTimeline.Create(duration, items);

        await Check.That(result)
            .IsNotNull()
            .And.Satisfies(x => x.Duration, assert => assert.IsZero())
            .And.Satisfies(x => x.Count, assert => assert.IsZero())
            .And.Satisfies(x => !(x.AsEnumerable()).Any(), assert => assert.IsTrue());
    }
    
    [Test]
    public async Task ZeroItems_ResultsIn_NoEvents()
    {
        const double duration = 1;
        var items = Array.Empty<TimelineItem<int>>();
        
        var result = EventTimeline.Create(duration, items);

        await Check.That(result)
            .IsNotNull()
            .And.Satisfies(x => x.Duration, assert => assert.IsEqualTo(duration))
            .And.Satisfies(x => x.Count, assert => assert.IsZero())
            .And.Satisfies(x => !(x.AsEnumerable()).Any(), assert => assert.IsTrue());
    }
    
    [Test]
    public async Task BeforeFirstItemDuration_ResultsIn_NoEvents()
    {
        const double duration = 1;
        var items = new TimelineItem<int>[]
        {
            new(1, 1)
        };
        
        var result = EventTimeline.Create(duration, items);

        await Check.That(result)
            .IsNotNull()
            .And.Satisfies(x => x.Duration, assert => assert.IsEqualTo(duration))
            .And.Satisfies(x => x.Count, assert => assert.IsZero())
            .And.Satisfies(x => !(x.AsEnumerable()).Any(), assert => assert.IsTrue());
    }
    
    [Test]
    public async Task NegativeDuration_ThrowsException()
    {
        const double duration = -1;
        var items = Array.Empty<TimelineItem<int>>();

        await Assert.That(() =>
        {
            EventTimeline.Create(duration, items);
        }).Throws<ArgumentOutOfRangeException>();
    }

    [Test]
    public async Task Items_AfterDuration_Trimmed()
    {
        const double duration = 1;
        var items = new TimelineItem<int>[]
        {
            new(0, 1),
            new(1, 2),
        };
        
        var result = EventTimeline.Create(duration, items);
        
        var expectedItems = new TimelineItem<int>[]
        {
            new(0, 1)
        };

        await Check.That(result)
            .IsNotNull()
            .And.Satisfies(x => x.Duration, assert => assert.IsEqualTo(duration))
            .And.Satisfies(x => x.Count, assert => assert.IsEqualTo(expectedItems.Length))
            .And.Satisfies(x => x.AsEnumerable(), assert => assert.IsEquivalentTo(expectedItems));
    }

    [Test]
    public async Task Items_OutOfOrder_Sorted()
    {
        const double duration = 2;
        var items = new TimelineItem<int>[]
        {
            new(1, 2),
            new(0, 1),
        };
        
        var result = EventTimeline.Create(duration, items);
        
        var expectedItems = new TimelineItem<int>[]
        {
            new(0, 1),
            new(1, 2),
        };

        await Check.That(result)
            .IsNotNull()
            .And.Satisfies(x => x.Duration, assert => assert.IsEqualTo(duration))
            .And.Satisfies(x => x.Count, assert => assert.IsEqualTo(expectedItems.Length))
            .And.Satisfies(x => x.AsEnumerable(), assert => assert.IsEquivalentTo(expectedItems));
    }
}
using Rmg.Core.Events;

namespace Rmg.Tests.EventTimelines;

public sealed class EventTimelineShiftTest
{
    [Test]
    [Arguments(0)]
    [Arguments(1)]
    public async Task ZeroDurationTimeline_Shifts_ToNoEvents(double offset)
    {
        var input = EventTimeline.Create<int>(0);
        
        var result = input.Shift(offset);

        await Check.That(result)
            .IsNotNull()
            .And.Satisfies(x => x.Duration, assert => assert.IsEqualTo(offset))
            .And.Satisfies(x => x.Count, assert => assert.IsZero())
            .And.Satisfies(x => !(x.AsEnumerable()).Any(), assert => assert.IsTrue());
    }
    
    [Test]
    [Arguments(-0.5)]
    [Arguments(-1)]
    public async Task NegativeOffsetPastEvents_Shifts_ToNoEvents(double offset)
    {
        const double duration = 1;
        var items = new TimelineItem<int>[]
        {
            new(0, 1)
        };
        
        var input = EventTimeline.Create(duration, items);
        
        var result = input.Shift(offset);

        await Check.That(result)
            .IsNotNull()
            .And.Satisfies(x => x.Duration, assert => assert.IsEqualTo(duration + offset))
            .And.Satisfies(x => x.Count, assert => assert.IsZero())
            .And.Satisfies(x => !(x.AsEnumerable()).Any(), assert => assert.IsTrue());
    }
    
    [Test]
    public async Task NegativeDurationShift_ThrowsException()
    {
        var input = EventTimeline.Create<int>(0);
        
        await Assert.That(() =>
        {
            input.Shift(-1);
        }).Throws<ArgumentOutOfRangeException>();
    }
    
    [Test]
    public async Task ZeroShift_SameItems()
    {
        const double duration = 1;
        var items = new TimelineItem<int>[]
        {
            new(0, 1)
        };
        
        var input = EventTimeline.Create(duration, items);
        
        var result = input.Shift(0);

        await Check.That(result)
            .IsNotNull()
            .And.Satisfies(x => x.Duration, assert => assert.IsEqualTo(duration))
            .And.Satisfies(x => x.Count, assert => assert.IsEqualTo(items.Length))
            .And.Satisfies(x => x.AsEnumerable(), assert => assert.IsEquivalentTo(items));
    }
    
    [Test]
    public async Task PositiveShift_ShiftsItems()
    {
        const double duration = 1;
        var items = new TimelineItem<int>[]
        {
            new(0, 1)
        };
        
        var input = EventTimeline.Create(duration, items);
        
        var result = input.Shift(1);

        const double expectedDuration = 2;
        var expectedItems = new TimelineItem<int>[]
        {
            new(1, 1)
        };

        await Check.That(result)
            .IsNotNull()
            .And.Satisfies(x => x.Duration, assert => assert.IsEqualTo(expectedDuration))
            .And.Satisfies(x => x.Count, assert => assert.IsEqualTo(expectedItems.Length))
            .And.Satisfies(x => x.AsEnumerable(), assert => assert.IsEquivalentTo(expectedItems));
    }
    
    [Test]
    public async Task PartialNegativeShift_SlicesItems()
    {
        const double duration = 2;
        var items = new TimelineItem<int>[]
        {
            new(0, 1),
            new(1, 2),
        };
        
        var input = EventTimeline.Create(duration, items);
        
        var result = input.Shift(-1);

        const double expectedDuration = 1;
        var expectedItems = new TimelineItem<int>[]
        {
            new(0, 2)
        };

        await Check.That(result)
            .IsNotNull()
            .And.Satisfies(x => x.Duration, assert => assert.IsEqualTo(expectedDuration))
            .And.Satisfies(x => x.Count, assert => assert.IsEqualTo(expectedItems.Length))
            .And.Satisfies(x => x.AsEnumerable(), assert => assert.IsEquivalentTo(expectedItems));
    }
}
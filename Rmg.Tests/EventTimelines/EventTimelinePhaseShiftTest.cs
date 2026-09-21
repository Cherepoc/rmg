using Rmg.Core.Events;

namespace Rmg.Tests.EventTimelines;

public sealed class EventTimelinePhaseShiftTest
{
    [Test]
    [Arguments(0)]
    [Arguments(1)]
    public async Task Empty_PhaseShifts_ToEmpty(double phase)
    {
        var input = EventTimeline.Empty<int>();
        
        var result = input.PhaseShift(phase);

        await Check.That(result)
            .IsNotNull()
            .And.Satisfies(x => x.IsEmpty, assert => assert.IsTrue())
            .And.Satisfies(x => x.Duration, assert => assert.IsZero())
            .And.Satisfies(x => x.Count, assert => assert.IsZero())
            .And.Satisfies(x => !(x.AsEnumerable()).Any(), assert => assert.IsTrue());
    }
    
    [Test]
    public async Task ZeroPhaseShift_SameItems()
    {
        const double duration = 1;
        var items = new TimelineItem<int>[]
        {
            new(0, 1)
        };
        
        var input = EventTimeline.Create(duration, items);
        
        var result = input.PhaseShift(0);

        await Check.That(result)
            .IsNotNull()
            .And.Satisfies(x => x.IsEmpty, assert => assert.IsFalse())
            .And.Satisfies(x => x.Duration, assert => assert.IsEqualTo(duration))
            .And.Satisfies(x => x.Count, assert => assert.IsEqualTo(items.Length))
            .And.Satisfies(x => x.AsEnumerable(), assert => assert.IsEquivalentTo(items));
    }
    
    [Test]
    [Arguments(0.5)]
    [Arguments(-2.5)]
    public async Task NoBreakForwardPhaseShift_ShiftsItems(double phase)
    {
        const double duration = 3;
        var items = new TimelineItem<int>[]
        {
            new(0, 1),
            new(1, 2),
            new(2, 3),
        };
        
        var input = EventTimeline.Create(duration, items);
        
        var result = input.PhaseShift(phase);

        var expectedItems = new TimelineItem<int>[]
        {
            new(0.5, 1),
            new(1.5, 2),
            new(2.5, 3),
        };

        await Check.That(result)
            .IsNotNull()
            .And.Satisfies(x => x.IsEmpty, assert => assert.IsFalse())
            .And.Satisfies(x => x.Duration, assert => assert.IsEqualTo(duration))
            .And.Satisfies(x => x.Count, assert => assert.IsEqualTo(expectedItems.Length))
            .And.Satisfies(x => x.AsEnumerable(), assert => assert.IsEquivalentTo(expectedItems));
    }
    
    [Test]
    [Arguments(-0.5)]
    [Arguments(2.5)]
    public async Task NoBreakBackwardsPhaseShift_ShiftsItems(double phase)
    {
        const double duration = 3;
        var items = new TimelineItem<int>[]
        {
            new(0.5, 1),
            new(1.5, 2),
            new(2.5, 3),
        };
        
        var input = EventTimeline.Create(duration, items);
        
        var result = input.PhaseShift(phase);

        var expectedItems = new TimelineItem<int>[]
        {
            new(0, 1),
            new(1, 2),
            new(2, 3),
        };

        await Check.That(result)
            .IsNotNull()
            .And.Satisfies(x => x.IsEmpty, assert => assert.IsFalse())
            .And.Satisfies(x => x.Duration, assert => assert.IsEqualTo(duration))
            .And.Satisfies(x => x.Count, assert => assert.IsEqualTo(expectedItems.Length))
            .And.Satisfies(x => x.AsEnumerable(), assert => assert.IsEquivalentTo(expectedItems));
    }
    
    [Test]
    [Arguments(1)]
    [Arguments(-2)]
    public async Task BreakOnItemForwardPhaseShift_ShiftsItems(double phase)
    {
        const double duration = 3;
        var items = new TimelineItem<int>[]
        {
            new(0, 1),
            new(1, 2),
            new(2, 3),
        };
        
        var input = EventTimeline.Create(duration, items);
        
        var result = input.PhaseShift(phase);

        var expectedItems = new TimelineItem<int>[]
        {
            new(0, 3),
            new(1, 1),
            new(2, 2),
        };

        await Check.That(result)
            .IsNotNull()
            .And.Satisfies(x => x.IsEmpty, assert => assert.IsFalse())
            .And.Satisfies(x => x.Duration, assert => assert.IsEqualTo(duration))
            .And.Satisfies(x => x.Count, assert => assert.IsEqualTo(expectedItems.Length))
            .And.Satisfies(x => x.AsEnumerable(), assert => assert.IsEquivalentTo(expectedItems));
    }
    
    [Test]
    [Arguments(-1)]
    [Arguments(2)]
    public async Task BreakOnItemBackwardPhaseShift_ShiftsItems(double phase)
    {
        const double duration = 3;
        var items = new TimelineItem<int>[]
        {
            new(0, 1),
            new(1, 2),
            new(2, 3),
        };
        
        var input = EventTimeline.Create(duration, items);
        
        var result = input.PhaseShift(phase);

        var expectedItems = new TimelineItem<int>[]
        {
            new(0, 2),
            new(1, 3),
            new(2, 1),
        };

        await Check.That(result)
            .IsNotNull()
            .And.Satisfies(x => x.IsEmpty, assert => assert.IsFalse())
            .And.Satisfies(x => x.Duration, assert => assert.IsEqualTo(duration))
            .And.Satisfies(x => x.Count, assert => assert.IsEqualTo(expectedItems.Length))
            .And.Satisfies(x => x.AsEnumerable(), assert => assert.IsEquivalentTo(expectedItems));
    }
    
    [Test]
    [Arguments(1.5)]
    [Arguments(-2.5)]
    public async Task BreakOffItemForwardPhaseShift_ShiftsItems(double phase)
    {
        const double duration = 4;
        var items = new TimelineItem<int>[]
        {
            new(0, 1),
            new(1, 2),
            new(2, 3),
            new(3, 4),
        };
        
        var input = EventTimeline.Create(duration, items);
        
        var result = input.PhaseShift(phase);

        var expectedItems = new TimelineItem<int>[]
        {
            new(0.5, 4),
            new(1.5, 1),
            new(2.5, 2),
            new(3.5, 3),
        };

        await Check.That(result)
            .IsNotNull()
            .And.Satisfies(x => x.IsEmpty, assert => assert.IsFalse())
            .And.Satisfies(x => x.Duration, assert => assert.IsEqualTo(duration))
            .And.Satisfies(x => x.Count, assert => assert.IsEqualTo(expectedItems.Length))
            .And.Satisfies(x => x.AsEnumerable(), assert => assert.IsEquivalentTo(expectedItems));
    }
    
    [Test]
    [Arguments(-0.5)]
    [Arguments(3.5)]
    public async Task BreakOffItemBackwardPhaseShift_ShiftsItems(double phase)
    {
        const double duration = 4;
        var items = new TimelineItem<int>[]
        {
            new(0, 1),
            new(1, 2),
            new(2, 3),
            new(3, 4),
        };
        
        var input = EventTimeline.Create(duration, items);
        
        var result = input.PhaseShift(phase);

        var expectedItems = new TimelineItem<int>[]
        {
            new(0.5, 2),
            new(1.5, 3),
            new(2.5, 4),
            new(3.5, 1),
        };

        await Check.That(result)
            .IsNotNull()
            .And.Satisfies(x => x.IsEmpty, assert => assert.IsFalse())
            .And.Satisfies(x => x.Duration, assert => assert.IsEqualTo(duration))
            .And.Satisfies(x => x.Count, assert => assert.IsEqualTo(expectedItems.Length))
            .And.Satisfies(x => x.AsEnumerable(), assert => assert.IsEquivalentTo(expectedItems));
    }
}
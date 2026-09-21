using Rmg.Core.Events;

namespace Rmg.Tests.StateTimelines;

public sealed class StateTimelineShiftTest
{
    private static readonly StateKind<int> StateKind = StateKinds.KeyOffset;
    
    [Test]
    public async Task Empty_ResultsIn_Empty()
    {
        var input = StateKind.EmptyTimeline;
        
        var result = input.Shift(1);

        await Check.That(result)
            .IsNotNull()
            .And.Satisfies(x => x.IsEmpty, assert => assert.IsTrue())
            .And.Satisfies(x => x.Duration, assert => assert.IsZero())
            .And.Satisfies(x => x.Count, assert => assert.IsZero())
            .And.Satisfies(x => !(x.AsEnumerable()).Any(), assert => assert.IsTrue());
    }
    
    [Test]
    public async Task OffsetToZeroDuration_ResultsIn_Empty()
    {
        const double duration = 1;
        var items = new TimelineItem<int>[]
        {
            new(0, 1)
        };
        
        var input = StateTimeline.Create(duration, StateKind, items);
        
        var result = input.Shift(-1);

        await Check.That(result)
            .IsNotNull()
            .And.Satisfies(x => x.IsEmpty, assert => assert.IsTrue())
            .And.Satisfies(x => x.Duration, assert => assert.IsZero())
            .And.Satisfies(x => x.Count, assert => assert.IsZero())
            .And.Satisfies(x => !(x.AsEnumerable()).Any(), assert => assert.IsTrue());
    }
    
    [Test]
    public async Task OffsetToNegativeDuration_ResultsIn_Exception()
    {
        var input = StateKind.EmptyTimeline;
        
        await Assert.That(() =>
        {
            input.Shift(-1);
        }).Throws<ArgumentOutOfRangeException>();
    }
    
    [Test]
    public async Task ZeroOffset_ResultsIn_NoChanges()
    {
        const double duration = 1;
        var items = new TimelineItem<int>[]
        {
            new(0, 1)
        };
        
        var input = StateTimeline.Create(duration, StateKind, items);
        
        var result = input.Shift(0);

        await Check.That(result)
            .IsNotNull()
            .And.Satisfies(x => x.IsEmpty, assert => assert.IsFalse())
            .And.Satisfies(x => x.Duration, assert => assert.IsEqualTo(duration))
            .And.Satisfies(x => x.Count, assert => assert.IsEqualTo(items.Length))
            .And.Satisfies(x => x.AsEnumerable(), assert => assert.IsEquivalentTo(items));
    }
    
    [Test]
    public async Task PositiveOffset_ResultsIn_ShiftedItems()
    {
        const double duration = 1;
        var items = new TimelineItem<int>[]
        {
            new(0, 1)
        };
        
        var input = StateTimeline.Create(duration, StateKind, items);
        
        var result = input.Shift(1);

        const double expectedDuration = 2;
        var expectedItems = new TimelineItem<int>[]
        {
            new(1, 1)
        };

        await Check.That(result)
            .IsNotNull()
            .And.Satisfies(x => x.IsEmpty, assert => assert.IsFalse())
            .And.Satisfies(x => x.Duration, assert => assert.IsEqualTo(expectedDuration))
            .And.Satisfies(x => x.Count, assert => assert.IsEqualTo(expectedItems.Length))
            .And.Satisfies(x => x.AsEnumerable(), assert => assert.IsEquivalentTo(expectedItems));
    }
    
    [Test]
    public async Task NegativeOffsetOnItem_ResultsIn_SlicedItems()
    {
        const double duration = 2;
        var items = new TimelineItem<int>[]
        {
            new(0, 1),
            new(1, 2),
        };
        
        var input = StateTimeline.Create(duration, StateKind, items);
        
        var result = input.Shift(-1);

        const double expectedDuration = 1;
        var expectedItems = new TimelineItem<int>[]
        {
            new(0, 2)
        };

        await Check.That(result)
            .IsNotNull()
            .And.Satisfies(x => x.IsEmpty, assert => assert.IsFalse())
            .And.Satisfies(x => x.Duration, assert => assert.IsEqualTo(expectedDuration))
            .And.Satisfies(x => x.Count, assert => assert.IsEqualTo(expectedItems.Length))
            .And.Satisfies(x => x.AsEnumerable(), assert => assert.IsEquivalentTo(expectedItems));
    }
    
    [Test]
    public async Task NegativeOffsetBetweenItems_ResultsIn_SlicedItemsWithSavedState()
    {
        const double duration = 2;
        var items = new TimelineItem<int>[]
        {
            new(0, 1),
            new(1, 2),
        };
        
        var input = StateTimeline.Create(duration, StateKind, items);
        
        var result = input.Shift(-0.5);

        const double expectedDuration = 1.5;
        var expectedItems = new TimelineItem<int>[]
        {
            new(0, 1),
            new(0.5, 2)
        };

        await Check.That(result)
            .IsNotNull()
            .And.Satisfies(x => x.IsEmpty, assert => assert.IsFalse())
            .And.Satisfies(x => x.Duration, assert => assert.IsEqualTo(expectedDuration))
            .And.Satisfies(x => x.Count, assert => assert.IsEqualTo(expectedItems.Length))
            .And.Satisfies(x => x.AsEnumerable(), assert => assert.IsEquivalentTo(expectedItems));
    }
    
    [Test]
    public async Task NegativeOffsetAfterItems_ResultsIn_SlicedItemsWithSavedState()
    {
        const double duration = 2;
        var items = new TimelineItem<int>[]
        {
            new(0, 1),
            new(1, 2),
        };
        
        var input = StateTimeline.Create(duration, StateKind, items);
        
        var result = input.Shift(-1.5);

        const double expectedDuration = 0.5;
        var expectedItems = new TimelineItem<int>[]
        {
            new(0, 2)
        };

        await Check.That(result)
            .IsNotNull()
            .And.Satisfies(x => x.IsEmpty, assert => assert.IsFalse())
            .And.Satisfies(x => x.Duration, assert => assert.IsEqualTo(expectedDuration))
            .And.Satisfies(x => x.Count, assert => assert.IsEqualTo(expectedItems.Length))
            .And.Satisfies(x => x.AsEnumerable(), assert => assert.IsEquivalentTo(expectedItems));
    }
    
    [Test]
    public async Task NegativeOffsetAfterDefaultItem_ResultsIn_SlicedItemsWithDefaultStateTrimmed()
    {
        const double duration = 3;
        var items = new TimelineItem<int>[]
        {
            new(0, 1),
            new(1, 0),
            new(2, 2),
        };
        
        var input = StateTimeline.Create(duration, StateKind, items);
        
        var result = input.Shift(-1);

        const double expectedDuration = 2;
        var expectedItems = new TimelineItem<int>[]
        {
            new(1, 2)
        };

        await Check.That(result)
            .IsNotNull()
            .And.Satisfies(x => x.IsEmpty, assert => assert.IsFalse())
            .And.Satisfies(x => x.Duration, assert => assert.IsEqualTo(expectedDuration))
            .And.Satisfies(x => x.Count, assert => assert.IsEqualTo(expectedItems.Length))
            .And.Satisfies(x => x.AsEnumerable(), assert => assert.IsEquivalentTo(expectedItems));
    }
    
    [Test]
    public async Task NegativeOffsetAfterLastDefaultItem_ResultsIn_Empty()
    {
        const double duration = 3;
        var items = new TimelineItem<int>[]
        {
            new(0, 1),
            new(1, 2),
            new(2, 0),
        };
        
        var input = StateTimeline.Create(duration, StateKind, items);
        
        var result = input.Shift(-2);

        await Check.That(result)
            .IsNotNull()
            .And.Satisfies(x => x.IsEmpty, assert => assert.IsTrue())
            .And.Satisfies(x => x.Duration, assert => assert.IsZero())
            .And.Satisfies(x => x.Count, assert => assert.IsZero())
            .And.Satisfies(x => !(x.AsEnumerable()).Any(), assert => assert.IsTrue());
    }
}
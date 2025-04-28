using Rmg.Core.Events;

namespace Rmg.Tests.StateTimelines;

public sealed class StateTimelinePhaseShiftTest
{
    private static readonly StateKind<int> StateKind = StateKinds.KeyOffset;
    
    [Test]
    [Arguments(0)]
    [Arguments(1)]
    public async Task Empty_ResultsIn_ToEmpty(double phase)
    {
        var input = StateKind.EmptyTimeline;
        
        var result = input.PhaseShift(phase);

        await Assert.That(result)
            .IsNotNull()
            .And.Satisfies(x => x.IsEmpty, assert => assert.IsTrue())
            .And.Satisfies(x => x.Duration, assert => assert.IsZero())
            .And.Satisfies(x => x.Count, assert => assert.IsZero())
            .And.Satisfies(x => x.AsEnumerable(), assert => assert.IsEmpty());
    }
    
    [Test]
    public async Task ZeroPhase_ResultsIn_NoChanges()
    {
        const double duration = 1;
        var items = new TimelineItem<int>[]
        {
            new(0, 1)
        };
        
        var input = StateTimeline.Create(duration, StateKind, items);
        
        var result = input.PhaseShift(0);

        await Assert.That(result)
            .IsNotNull()
            .And.Satisfies(x => x.IsEmpty, assert => assert.IsFalse())
            .And.Satisfies(x => x.Duration, assert => assert.IsEqualTo(duration))
            .And.Satisfies(x => x.Count, assert => assert.IsEqualTo(items.Length))
            .And.Satisfies(x => x.AsEnumerable(), assert => assert.IsEquivalentTo(items));
    }
    
    [Test]
    [Arguments(0.5)]
    [Arguments(-3.5)]
    public async Task NoBreakForwardPhase_WithDefaultStates_ResultsIn_ShiftedItems(double phase)
    {
        const double duration = 4;
        var items = new TimelineItem<int>[]
        {
            new(1, 1),
            new(2, 2),
            new(3, 0),
        };
        
        var input = StateTimeline.Create(duration, StateKind, items);
        
        var result = input.PhaseShift(phase);

        var expectedItems = new TimelineItem<int>[]
        {
            new(1.5, 1),
            new(2.5, 2),
            new(3.5, 0),
        };

        await Assert.That(result)
            .IsNotNull()
            .And.Satisfies(x => x.IsEmpty, assert => assert.IsFalse())
            .And.Satisfies(x => x.Duration, assert => assert.IsEqualTo(duration))
            .And.Satisfies(x => x.Count, assert => assert.IsEqualTo(expectedItems.Length))
            .And.Satisfies(x => x.AsEnumerable(), assert => assert.IsEquivalentTo(expectedItems));
    }
    
    [Test]
    [Arguments(-0.5)]
    [Arguments(3.5)]
    public async Task NoBreakBackwardPhase_WithDefaultStates_ResultsIn_ShiftedItems(double phase)
    {
        const double duration = 4;
        var items = new TimelineItem<int>[]
        {
            new(1, 1),
            new(2, 2),
            new(3, 0),
        };
        
        var input = StateTimeline.Create(duration, StateKind, items);
        
        var result = input.PhaseShift(phase);

        var expectedItems = new TimelineItem<int>[]
        {
            new(0.5, 1),
            new(1.5, 2),
            new(2.5, 0),
        };

        await Assert.That(result)
            .IsNotNull()
            .And.Satisfies(x => x.IsEmpty, assert => assert.IsFalse())
            .And.Satisfies(x => x.Duration, assert => assert.IsEqualTo(duration))
            .And.Satisfies(x => x.Count, assert => assert.IsEqualTo(expectedItems.Length))
            .And.Satisfies(x => x.AsEnumerable(), assert => assert.IsEquivalentTo(expectedItems));
    }
    
    [Test]
    [Arguments(0.5)]
    [Arguments(-2.5)]
    public async Task NoBreakForwardPhase_WithoutDefaultStates_ResultsIn_ShiftedItemsWithSavedState(double phase)
    {
        const double duration = 3;
        var items = new TimelineItem<int>[]
        {
            new(0, 1),
            new(1, 2),
            new(2, 3),
        };
        
        var input = StateTimeline.Create(duration, StateKind, items);
        
        var result = input.PhaseShift(phase);

        var expectedItems = new TimelineItem<int>[]
        {
            new(0, 3),
            new(0.5, 1),
            new(1.5, 2),
            new(2.5, 3),
        };

        await Assert.That(result)
            .IsNotNull()
            .And.Satisfies(x => x.IsEmpty, assert => assert.IsFalse())
            .And.Satisfies(x => x.Duration, assert => assert.IsEqualTo(duration))
            .And.Satisfies(x => x.Count, assert => assert.IsEqualTo(expectedItems.Length))
            .And.Satisfies(x => x.AsEnumerable(), assert => assert.IsEquivalentTo(expectedItems));
    }
    
    [Test]
    [Arguments(-0.5)]
    [Arguments(2.5)]
    public async Task NoBreakBackwardPhase_WithoutDefaultStates_ResultsIn_ShiftedItemsWithSavedState(double phase)
    {
        const double duration = 3;
        var items = new TimelineItem<int>[]
        {
            new(0, 1),
            new(1, 2),
            new(2, 3),
        };
        
        var input = StateTimeline.Create(duration, StateKind, items);
        
        var result = input.PhaseShift(phase);

        var expectedItems = new TimelineItem<int>[]
        {
            new(0, 1),
            new(0.5, 2),
            new(1.5, 3),
            new(2.5, 1),
        };

        await Assert.That(result)
            .IsNotNull()
            .And.Satisfies(x => x.IsEmpty, assert => assert.IsFalse())
            .And.Satisfies(x => x.Duration, assert => assert.IsEqualTo(duration))
            .And.Satisfies(x => x.Count, assert => assert.IsEqualTo(expectedItems.Length))
            .And.Satisfies(x => x.AsEnumerable(), assert => assert.IsEquivalentTo(expectedItems));
    }
    
    [Test]
    [Arguments(1)]
    [Arguments(-2)]
    public async Task BreakOnItemForwardPhase_WithoutDefaultState_ResultsIn_ShiftedItems(double phase)
    {
        const double duration = 3;
        var items = new TimelineItem<int>[]
        {
            new(0, 1),
            new(1, 2),
            new(2, 3),
        };
        
        var input = StateTimeline.Create(duration, StateKind, items);
        
        var result = input.PhaseShift(phase);

        var expectedItems = new TimelineItem<int>[]
        {
            new(0, 3),
            new(1, 1),
            new(2, 2),
        };

        await Assert.That(result)
            .IsNotNull()
            .And.Satisfies(x => x.IsEmpty, assert => assert.IsFalse())
            .And.Satisfies(x => x.Duration, assert => assert.IsEqualTo(duration))
            .And.Satisfies(x => x.Count, assert => assert.IsEqualTo(expectedItems.Length))
            .And.Satisfies(x => x.AsEnumerable(), assert => assert.IsEquivalentTo(expectedItems));
    }
    
    [Test]
    [Arguments(1)]
    [Arguments(-2)]
    public async Task BreakOnItemForwardPhase_WithDefaultState_ResultsIn_ShiftedItemsWithTrimmedDefaultState(double phase)
    {
        const double duration = 3;
        var items = new TimelineItem<int>[]
        {
            new(0, 1),
            new(1, 2),
            new(2, 0),
        };
        
        var input = StateTimeline.Create(duration, StateKind, items);
        
        var result = input.PhaseShift(phase);

        var expectedItems = new TimelineItem<int>[]
        {
            new(1, 1),
            new(2, 2),
        };

        await Assert.That(result)
            .IsNotNull()
            .And.Satisfies(x => x.IsEmpty, assert => assert.IsFalse())
            .And.Satisfies(x => x.Duration, assert => assert.IsEqualTo(duration))
            .And.Satisfies(x => x.Count, assert => assert.IsEqualTo(expectedItems.Length))
            .And.Satisfies(x => x.AsEnumerable(), assert => assert.IsEquivalentTo(expectedItems));
    }
    
    [Test]
    [Arguments(1)]
    [Arguments(-2)]
    public async Task BreakOnItemForwardPhase_WithEqualState_ResultsIn_ShiftedItemsWithNoEqualState(double phase)
    {
        const double duration = 3;
        var items = new TimelineItem<int>[]
        {
            new(0, 1),
            new(1, 2),
            new(2, 1),
        };
        
        var input = StateTimeline.Create(duration, StateKind, items);
        
        var result = input.PhaseShift(phase);

        var expectedItems = new TimelineItem<int>[]
        {
            new(0, 1),
            new(2, 2),
        };

        await Assert.That(result)
            .IsNotNull()
            .And.Satisfies(x => x.IsEmpty, assert => assert.IsFalse())
            .And.Satisfies(x => x.Duration, assert => assert.IsEqualTo(duration))
            .And.Satisfies(x => x.Count, assert => assert.IsEqualTo(expectedItems.Length))
            .And.Satisfies(x => x.AsEnumerable(), assert => assert.IsEquivalentTo(expectedItems));
    }
    
    [Test]
    [Arguments(-1)]
    [Arguments(2)]
    public async Task BreakOnItemBackwardPhase_WithoutDefaultState_ResultsIn_ShiftedItems(double phase)
    {
        const double duration = 3;
        var items = new TimelineItem<int>[]
        {
            new(0, 1),
            new(1, 2),
            new(2, 3),
        };
        
        var input = StateTimeline.Create(duration, StateKind, items);
        
        var result = input.PhaseShift(phase);

        var expectedItems = new TimelineItem<int>[]
        {
            new(0, 2),
            new(1, 3),
            new(2, 1),
        };

        await Assert.That(result)
            .IsNotNull()
            .And.Satisfies(x => x.IsEmpty, assert => assert.IsFalse())
            .And.Satisfies(x => x.Duration, assert => assert.IsEqualTo(duration))
            .And.Satisfies(x => x.Count, assert => assert.IsEqualTo(expectedItems.Length))
            .And.Satisfies(x => x.AsEnumerable(), assert => assert.IsEquivalentTo(expectedItems));
    }
    
    [Test]
    [Arguments(-1)]
    [Arguments(2)]
    public async Task BreakOnItemBackwardPhase_WithDefaultState_ResultsIn_ShiftedItemsWithTrimmedDefaultState(double phase)
    {
        const double duration = 3;
        var items = new TimelineItem<int>[]
        {
            new(0, 1),
            new(1, 0),
            new(2, 3),
        };
        
        var input = StateTimeline.Create(duration, StateKind, items);
        
        var result = input.PhaseShift(phase);

        var expectedItems = new TimelineItem<int>[]
        {
            new(1, 3),
            new(2, 1),
        };

        await Assert.That(result)
            .IsNotNull()
            .And.Satisfies(x => x.IsEmpty, assert => assert.IsFalse())
            .And.Satisfies(x => x.Duration, assert => assert.IsEqualTo(duration))
            .And.Satisfies(x => x.Count, assert => assert.IsEqualTo(expectedItems.Length))
            .And.Satisfies(x => x.AsEnumerable(), assert => assert.IsEquivalentTo(expectedItems));
    }
    
    [Test]
    [Arguments(-1)]
    [Arguments(2)]
    public async Task BreakOnItemBackwardPhase_WithEqualState_ResultsIn_ShiftedItemsWithNoEqualState(double phase)
    {
        const double duration = 3;
        var items = new TimelineItem<int>[]
        {
            new(0, 1),
            new(1, 2),
            new(2, 1),
        };
        
        var input = StateTimeline.Create(duration, StateKind, items);
        
        var result = input.PhaseShift(phase);

        var expectedItems = new TimelineItem<int>[]
        {
            new(0, 2),
            new(1, 1),
        };

        await Assert.That(result)
            .IsNotNull()
            .And.Satisfies(x => x.IsEmpty, assert => assert.IsFalse())
            .And.Satisfies(x => x.Duration, assert => assert.IsEqualTo(duration))
            .And.Satisfies(x => x.Count, assert => assert.IsEqualTo(expectedItems.Length))
            .And.Satisfies(x => x.AsEnumerable(), assert => assert.IsEquivalentTo(expectedItems));
    }
    
    [Test]
    [Arguments(1.5)]
    [Arguments(-2.5)]
    public async Task BreakOffItemForwardPhase_WithoutDefaultState_ResultsIn_ShiftedItems(double phase)
    {
        const double duration = 4;
        var items = new TimelineItem<int>[]
        {
            new(0, 1),
            new(1, 2),
            new(2, 3),
            new(3, 4),
        };
        
        var input = StateTimeline.Create(duration, StateKind, items);
        
        var result = input.PhaseShift(phase);

        var expectedItems = new TimelineItem<int>[]
        {
            new(0, 3),
            new(0.5, 4),
            new(1.5, 1),
            new(2.5, 2),
            new(3.5, 3),
        };

        await Assert.That(result)
            .IsNotNull()
            .And.Satisfies(x => x.IsEmpty, assert => assert.IsFalse())
            .And.Satisfies(x => x.Duration, assert => assert.IsEqualTo(duration))
            .And.Satisfies(x => x.Count, assert => assert.IsEqualTo(expectedItems.Length))
            .And.Satisfies(x => x.AsEnumerable(), assert => assert.IsEquivalentTo(expectedItems));
    }
    
    [Test]
    [Arguments(1.5)]
    [Arguments(-2.5)]
    public async Task BreakOffItemForwardPhase_WithDefaultState_ResultsIn_ShiftedItemsWithNoDefaultState(double phase)
    {
        const double duration = 4;
        var items = new TimelineItem<int>[]
        {
            new(0, 1),
            new(1, 2),
            new(2, 0),
            new(3, 4),
        };
        
        var input = StateTimeline.Create(duration, StateKind, items);
        
        var result = input.PhaseShift(phase);

        var expectedItems = new TimelineItem<int>[]
        {
            new(0.5, 4),
            new(1.5, 1),
            new(2.5, 2),
            new(3.5, 0),
        };

        await Assert.That(result)
            .IsNotNull()
            .And.Satisfies(x => x.IsEmpty, assert => assert.IsFalse())
            .And.Satisfies(x => x.Duration, assert => assert.IsEqualTo(duration))
            .And.Satisfies(x => x.Count, assert => assert.IsEqualTo(expectedItems.Length))
            .And.Satisfies(x => x.AsEnumerable(), assert => assert.IsEquivalentTo(expectedItems));
    }
    
    [Test]
    [Arguments(1.5)]
    [Arguments(-2.5)]
    public async Task BreakOffItemForwardPhase_WithEqualState_ResultsIn_ShiftedItemsWithNoEqualState(double phase)
    {
        const double duration = 4;
        var items = new TimelineItem<int>[]
        {
            new(0, 1),
            new(1, 2),
            new(2, 3),
            new(3, 1),
        };
        
        var input = StateTimeline.Create(duration, StateKind, items);
        
        var result = input.PhaseShift(phase);

        var expectedItems = new TimelineItem<int>[]
        {
            new(0, 3),
            new(0.5, 1),
            new(2.5, 2),
            new(3.5, 3),
        };

        await Assert.That(result)
            .IsNotNull()
            .And.Satisfies(x => x.IsEmpty, assert => assert.IsFalse())
            .And.Satisfies(x => x.Duration, assert => assert.IsEqualTo(duration))
            .And.Satisfies(x => x.Count, assert => assert.IsEqualTo(expectedItems.Length))
            .And.Satisfies(x => x.AsEnumerable(), assert => assert.IsEquivalentTo(expectedItems));
    }
    
    [Test]
    [Arguments(-1.5)]
    [Arguments(2.5)]
    public async Task BreakOffItemBackwardPhase_WithoutDefaultState_ResultsIn_ShiftedItems(double phase)
    {
        const double duration = 4;
        var items = new TimelineItem<int>[]
        {
            new(0, 1),
            new(1, 2),
            new(2, 3),
            new(3, 4),
        };
        
        var input = StateTimeline.Create(duration, StateKind, items);
        
        var result = input.PhaseShift(phase);

        var expectedItems = new TimelineItem<int>[]
        {
            new(0, 2),
            new(0.5, 3),
            new(1.5, 4),
            new(2.5, 1),
            new(3.5, 2),
        };

        await Assert.That(result)
            .IsNotNull()
            .And.Satisfies(x => x.IsEmpty, assert => assert.IsFalse())
            .And.Satisfies(x => x.Duration, assert => assert.IsEqualTo(duration))
            .And.Satisfies(x => x.Count, assert => assert.IsEqualTo(expectedItems.Length))
            .And.Satisfies(x => x.AsEnumerable(), assert => assert.IsEquivalentTo(expectedItems));
    }
    
    [Test]
    [Arguments(-1.5)]
    [Arguments(2.5)]
    public async Task BreakOffItemBackwardPhase_WithDefaultState_ResultsIn_ShiftedItemsWithNoDefaultState(double phase)
    {
        const double duration = 4;
        var items = new TimelineItem<int>[]
        {
            new(0, 1),
            new(1, 0),
            new(2, 3),
            new(3, 4),
        };
        
        var input = StateTimeline.Create(duration, StateKind, items);
        
        var result = input.PhaseShift(phase);

        var expectedItems = new TimelineItem<int>[]
        {
            new(0.5, 3),
            new(1.5, 4),
            new(2.5, 1),
            new(3.5, 0),
        };

        await Assert.That(result)
            .IsNotNull()
            .And.Satisfies(x => x.IsEmpty, assert => assert.IsFalse())
            .And.Satisfies(x => x.Duration, assert => assert.IsEqualTo(duration))
            .And.Satisfies(x => x.Count, assert => assert.IsEqualTo(expectedItems.Length))
            .And.Satisfies(x => x.AsEnumerable(), assert => assert.IsEquivalentTo(expectedItems));
    }
    
    [Test]
    [Arguments(-1.5)]
    [Arguments(2.5)]
    public async Task BreakOffItemBackwardPhase_WithEqualState_ResultsIn_ShiftedItemsWithNoEqualState(double phase)
    {
        const double duration = 4;
        var items = new TimelineItem<int>[]
        {
            new(0, 1),
            new(1, 2),
            new(2, 3),
            new(3, 1),
        };
        
        var input = StateTimeline.Create(duration, StateKind, items);
        
        var result = input.PhaseShift(phase);

        var expectedItems = new TimelineItem<int>[]
        {
            new(0, 2),
            new(0.5, 3),
            new(1.5, 1),
            new(3.5, 2),
        };

        await Assert.That(result)
            .IsNotNull()
            .And.Satisfies(x => x.IsEmpty, assert => assert.IsFalse())
            .And.Satisfies(x => x.Duration, assert => assert.IsEqualTo(duration))
            .And.Satisfies(x => x.Count, assert => assert.IsEqualTo(expectedItems.Length))
            .And.Satisfies(x => x.AsEnumerable(), assert => assert.IsEquivalentTo(expectedItems));
    }
}
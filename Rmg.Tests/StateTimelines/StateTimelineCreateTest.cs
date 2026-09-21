using Rmg.Core.Events;

namespace Rmg.Tests.StateTimelines;

public sealed class StateTimelineCreateTest
{
    private static readonly StateKind<int> StateKind = StateKinds.KeyOffset;
    
    [Test]
    public async Task ZeroDuration_ResultsIn_Empty()
    {
        const double duration = 0;
        var items = new TimelineItem<int>[]
        {
            new(0, 1)
        };
        
        var result = StateTimeline.Create(duration, StateKind, items);

        await Check.That(result)
            .IsNotNull()
            .And.Satisfies(x => x.IsEmpty, assert => assert.IsTrue())
            .And.Satisfies(x => x.Duration, assert => assert.IsZero())
            .And.Satisfies(x => x.Count, assert => assert.IsZero())
            .And.Satisfies(x => !(x.AsEnumerable()).Any(), assert => assert.IsTrue());
    }
    
    [Test]
    [Arguments(0)]
    [Arguments(1)]
    [Arguments(2)]
    public async Task DefaultItems_ResultsIn_Empty(int count)
    {
        const double duration = 3;
        var items = new TimelineItem<int>[count];
        for (int i = 0; i < count; i++)
            items[i] = 0.ToTimelineItem(i);
        
        var result = StateTimeline.Create(duration, StateKind, items);

        await Check.That(result)
            .IsNotNull()
            .And.Satisfies(x => x.IsEmpty, assert => assert.IsFalse())
            .And.Satisfies(x => x.Duration, assert => assert.IsEqualTo(duration))
            .And.Satisfies(x => x.Count, assert => assert.IsZero())
            .And.Satisfies(x => !(x.AsEnumerable()).Any(), assert => assert.IsTrue());
    }
    
    [Test]
    public async Task BeforeFirstNonDefaultItemDuration_NonEmpty()
    {
        const double duration = 1;
        var items = new TimelineItem<int>[]
        {
            
            new(0, 0),
            new(1, 1),
        };
        
        var result = StateTimeline.Create(duration, StateKind, items);

        await Check.That(result)
            .IsNotNull()
            .And.Satisfies(x => x.IsEmpty, assert => assert.IsFalse())
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
            StateTimeline.Create(duration, StateKind, items);
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
        
        var result = StateTimeline.Create(duration, StateKind, items);
        
        var expectedItems = new TimelineItem<int>[]
        {
            new(0, 1),
        };

        await Check.That(result)
            .IsNotNull()
            .And.Satisfies(x => x.IsEmpty, assert => assert.IsFalse())
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
        
        var result = StateTimeline.Create(duration, StateKind, items);
        
        var expectedItems = new TimelineItem<int>[]
        {
            new(0, 1),
            new(1, 2),
        };

        await Check.That(result)
            .IsNotNull()
            .And.Satisfies(x => x.IsEmpty, assert => assert.IsFalse())
            .And.Satisfies(x => x.Duration, assert => assert.IsEqualTo(duration))
            .And.Satisfies(x => x.Count, assert => assert.IsEqualTo(expectedItems.Length))
            .And.Satisfies(x => x.AsEnumerable(), assert => assert.IsEquivalentTo(expectedItems));
    }

    [Test]
    public async Task FirstDefaultItems_Trimmed()
    {
        const double duration = 3;
        var items = new TimelineItem<int>[]
        {
            new(0, 0),
            new(0.5, 0),
            new(1, 1),
            new(2, 0),
        };
        
        var result = StateTimeline.Create(duration, StateKind, items);
        
        var expectedItems = new TimelineItem<int>[]
        {
            new(1, 1),
            new(2, 0),
        };

        await Check.That(result)
            .IsNotNull()
            .And.Satisfies(x => x.IsEmpty, assert => assert.IsFalse())
            .And.Satisfies(x => x.Duration, assert => assert.IsEqualTo(duration))
            .And.Satisfies(x => x.Count, assert => assert.IsEqualTo(expectedItems.Length))
            .And.Satisfies(x => x.AsEnumerable(), assert => assert.IsEquivalentTo(expectedItems));
    }

    [Test]
    public async Task EqualConsecutiveItems_Trimmed()
    {
        const double duration = 6;
        var items = new TimelineItem<int>[]
        {
            new(0, 1),
            new(1, 2),
            new(2, 2),
            new(3, 1),
            new(4, 1),
            new(5, 2),
        };
        
        var result = StateTimeline.Create(duration, StateKind, items);
        
        var expectedItems = new TimelineItem<int>[]
        {
            new(0, 1),
            new(1, 2),
            new(3, 1),
            new(5, 2),
        };

        await Check.That(result)
            .IsNotNull()
            .And.Satisfies(x => x.IsEmpty, assert => assert.IsFalse())
            .And.Satisfies(x => x.Duration, assert => assert.IsEqualTo(duration))
            .And.Satisfies(x => x.Count, assert => assert.IsEqualTo(expectedItems.Length))
            .And.Satisfies(x => x.AsEnumerable(), assert => assert.IsEquivalentTo(expectedItems));
    }

    [Test]
    public async Task EqualPositionItems_Merged()
    {
        const double duration = 3;
        var items = new TimelineItem<int>[]
        {
            new(0, 1),
            new(0, 0),
            new(1, 1),
            new(1, 1),
            new(2, 1),
            new(2, 2),
            new(2, 3),
        };
        
        var result = StateTimeline.Create(duration, StateKind, items);
        
        var expectedItems = new TimelineItem<int>[]
        {
            new(0, 1),
            new(1, 2),
            new(2, 6),
        };

        await Check.That(result)
            .IsNotNull()
            .And.Satisfies(x => x.IsEmpty, assert => assert.IsFalse())
            .And.Satisfies(x => x.Duration, assert => assert.IsEqualTo(duration))
            .And.Satisfies(x => x.Count, assert => assert.IsEqualTo(expectedItems.Length))
            .And.Satisfies(x => x.AsEnumerable(), assert => assert.IsEquivalentTo(expectedItems));
    }
}
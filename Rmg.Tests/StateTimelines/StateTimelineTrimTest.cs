using Rmg.Core.Events;

namespace Rmg.Tests.StateTimelines;

public sealed class StateTimelineTrimTest
{
    private static readonly StateKind<int> StateKind = StateKinds.KeyOffset;
    
    [Test]
    public async Task ZeroDurationTimeline_ResultsIn_Default()
    {
        var input = StateKind.CreateDefaultTimeline(0);
        
        var result = input.Trim(1);

        await Check.That(result)
            .IsNotNull()
            .And.Satisfies(x => x.IsDefault, assert => assert.IsTrue())
            .And.Satisfies(x => x.Duration, assert => assert.IsEqualTo(1))
            .And.Satisfies(x => x.Count, assert => assert.IsZero())
            .And.Satisfies(x => !(x.AsEnumerable()).Any(), assert => assert.IsTrue());
    }
    
    [Test]
    public async Task ZeroDuration_ResultsIn_ZeroDuration()
    {
        const double duration = 1;
        var items = new TimelineItem<int>[]
        {
            new(0, 1)
        };
        
        var input = StateTimeline.Create(duration, StateKind, items);
        
        var result = input.Trim(0);

        await Check.That(result)
            .IsNotNull()
            .And.Satisfies(x => x.Duration, assert => assert.IsZero())
            .And.Satisfies(x => x.Count, assert => assert.IsZero())
            .And.Satisfies(x => !(x.AsEnumerable()).Any(), assert => assert.IsTrue());
    }
    
    [Test]
    public async Task BeforeFirstItemDuration_ResultsIn_Default()
    {
        const double duration = 2;
        var items = new TimelineItem<int>[]
        {
            new(1, 1)
        };
        
        var input = StateTimeline.Create(duration, StateKind, items);
        
        var result = input.Trim(1);

        await Check.That(result)
            .IsNotNull()
            .And.Satisfies(x => x.IsDefault, assert => assert.IsTrue())
            .And.Satisfies(x => x.Duration, assert => assert.IsEqualTo(1))
            .And.Satisfies(x => x.Count, assert => assert.IsZero())
            .And.Satisfies(x => !(x.AsEnumerable()).Any(), assert => assert.IsTrue());
    }

    [Test]
    public async Task NegativeDuration_ResultsIn_ThrownException()
    {
        var input = StateKind.CreateDefaultTimeline(0);

        await Assert.That(() =>
        {
            input.Trim(-1);
        }).Throws<ArgumentOutOfRangeException>();
    }
    
    [Test]
    public async Task SameDuration_ResultsIn_SameItems()
    {
        const double duration = 1;
        var items = new TimelineItem<int>[]
        {
            new(0, 1)
        };
        
        var input = StateTimeline.Create(duration, StateKind, items);
        
        var result = input.Trim(duration);

        await Check.That(result)
            .IsNotNull()
            .And.Satisfies(x => x.Duration, assert => assert.IsEqualTo(duration))
            .And.Satisfies(x => x.Count, assert => assert.IsEqualTo(items.Length))
            .And.Satisfies(x => x.AsEnumerable(), assert => assert.IsEquivalentTo(items));
    }
    
    [Test]
    public async Task LargerDuration_WithNonDefaultLastItem_ResultsIn_AddedDefaultState()
    {
        const double duration = 1;
        var items = new TimelineItem<int>[]
        {
            new(0, 1)
        };
        
        var input = StateTimeline.Create(duration, StateKind, items);
        
        const double newDuration = 2;
        var result = input.Trim(newDuration);
        
        var expectedItems = new TimelineItem<int>[]
        {
            new(0, 1),
            new(1, 0)
        };

        await Check.That(result)
            .IsNotNull()
            .And.Satisfies(x => x.Duration, assert => assert.IsEqualTo(newDuration))
            .And.Satisfies(x => x.Count, assert => assert.IsEqualTo(expectedItems.Length))
            .And.Satisfies(x => x.AsEnumerable(), assert => assert.IsEquivalentTo(expectedItems));
    }
    
    [Test]
    public async Task LargerDuration_WithDefaultLastItem_ResultsIn_NoAddedDefaultState()
    {
        const double duration = 1;
        var items = new TimelineItem<int>[]
        {
            new(0, 1),
            new(0.5, 0),
        };
        
        var input = StateTimeline.Create(duration, StateKind, items);
        
        const double newDuration = 2;
        var result = input.Trim(newDuration);

        await Check.That(result)
            .IsNotNull()
            .And.Satisfies(x => x.Duration, assert => assert.IsEqualTo(newDuration))
            .And.Satisfies(x => x.Count, assert => assert.IsEqualTo(items.Length))
            .And.Satisfies(x => x.AsEnumerable(), assert => assert.IsEquivalentTo(items));
    }
    
    [Test]
    public async Task PartialLowerDuration_ResultsIn_TrimmedItems()
    {
        const double duration = 2;
        var items = new TimelineItem<int>[]
        {
            new(0, 1),
            new(1, 2),
        };
        
        var input = StateTimeline.Create(duration, StateKind, items);
        
        const double newDuration = 1;
        var result = input.Trim(newDuration);
        
        var expectedItems = new TimelineItem<int>[]
        {
            new(0, 1)
        };

        await Check.That(result)
            .IsNotNull()
            .And.Satisfies(x => x.Duration, assert => assert.IsEqualTo(newDuration))
            .And.Satisfies(x => x.Count, assert => assert.IsEqualTo(expectedItems.Length))
            .And.Satisfies(x => x.AsEnumerable(), assert => assert.IsEquivalentTo(expectedItems));
    }
}
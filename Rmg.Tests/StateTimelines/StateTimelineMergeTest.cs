using Rmg.Core.Events;

namespace Rmg.Tests.StateTimelines;

public sealed class StateTimelineMergeTest
{
    private static readonly StateKind<int> StateKind = StateKinds.KeyOffset;
    
    [Test]
    [Arguments(0)]
    [Arguments(1)]
    [Arguments(2)]
    public async Task Empty_ResultsIn_Empty(int count)
    {
        var input = new StateTimeline<int>[count];
        for (var i = 0; i < count; i++)
            input[i] = StateKind.EmptyTimeline;
        
        var result = StateTimeline.Merge(input);

        await Check.That(result)
            .IsNotNull()
            .And.Satisfies(x => x.IsEmpty, assert => assert.IsTrue())
            .And.Satisfies(x => x.Duration, assert => assert.IsZero())
            .And.Satisfies(x => x.Count, assert => assert.IsZero())
            .And.Satisfies(x => !(x.AsEnumerable()).Any(), assert => assert.IsTrue());
    }
    
    [Test]
    public async Task SingleTimeline_ResultsIn_Itself()
    {
        const double duration = 2;
        var items = new TimelineItem<int>[]
        {
            new(0, 1),
            new(1, 2),
        };
        var timeline = StateTimeline.Create(duration, StateKind, items);

        var input = new[]
        {
            timeline
        };
        
        var result = StateTimeline.Merge(input);

        await Check.That(result)
            .IsNotNull()
            .And.Satisfies(x => x.IsEmpty, assert => assert.IsFalse())
            .And.Satisfies(x => x.Duration, assert => assert.IsEqualTo(duration))
            .And.Satisfies(x => x.Count, assert => assert.IsEqualTo(items.Length))
            .And.Satisfies(x => x.AsEnumerable(), assert => assert.IsEquivalentTo(items));
    }
    
    [Test]
    public async Task MultipleContinuousTimelines_ResultsIn_SingleTimeline()
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

        var input = new[]
        {
            timeline2,
            timeline1,
        };
        
        var result = StateTimeline.Merge(input);

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
            .And.Satisfies(x => x.IsEmpty, assert => assert.IsFalse())
            .And.Satisfies(x => x.Duration, assert => assert.IsEqualTo(expectedDuration))
            .And.Satisfies(x => x.Count, assert => assert.IsEqualTo(expectedItems.Length))
            .And.Satisfies(x => x.AsEnumerable(), assert => assert.IsEquivalentTo(expectedItems));
    }
    
    [Test]
    public async Task MultipleSeparatedTimelines_ResultsIn_SingleTimeline_WithAddedDefaultState()
    {
        var items1 = new TimelineItem<int>[]
        {
            new(0, 1),
        };
        var timeline1 = StateTimeline.Create(1, StateKind, items1);
        
        var items2 = new TimelineItem<int>[]
        {
            new(2, 3),
            new(3, 4),
        };
        var timeline2 = StateTimeline.Create(4, StateKind, items2);

        var input = new[]
        {
            timeline2,
            timeline1,
        };
        
        var result = StateTimeline.Merge(input);

        const double expectedDuration = 4;
        var expectedItems = new TimelineItem<int>[]
        {
            new(0, 1),
            new(1, 0),
            new(2, 3),
            new(3, 4),
        };

        await Check.That(result)
            .IsNotNull()
            .And.Satisfies(x => x.IsEmpty, assert => assert.IsFalse())
            .And.Satisfies(x => x.Duration, assert => assert.IsEqualTo(expectedDuration))
            .And.Satisfies(x => x.Count, assert => assert.IsEqualTo(expectedItems.Length))
            .And.Satisfies(x => x.AsEnumerable(), assert => assert.IsEquivalentTo(expectedItems));
    }
    
    [Test]
    public async Task MultipleSeparatedTimelines_WithDefaultState_ResultsIn_SingleTimeline_WithDefaultStateRemoved()
    {
        var items1 = new TimelineItem<int>[]
        {
            new(0, 1),
            new(0.5, 0),
        };
        var timeline1 = StateTimeline.Create(1, StateKind, items1);
        
        var items2 = new TimelineItem<int>[]
        {
            new(2, 3),
            new(3, 4),
        };
        var timeline2 = StateTimeline.Create(4, StateKind, items2);

        var input = new[]
        {
            timeline2,
            timeline1,
        };
        
        var result = StateTimeline.Merge(input);

        const double expectedDuration = 4;
        var expectedItems = new TimelineItem<int>[]
        {
            new(0, 1),
            new(0.5, 0),
            new(2, 3),
            new(3, 4),
        };

        await Check.That(result)
            .IsNotNull()
            .And.Satisfies(x => x.IsEmpty, assert => assert.IsFalse())
            .And.Satisfies(x => x.Duration, assert => assert.IsEqualTo(expectedDuration))
            .And.Satisfies(x => x.Count, assert => assert.IsEqualTo(expectedItems.Length))
            .And.Satisfies(x => x.AsEnumerable(), assert => assert.IsEquivalentTo(expectedItems));
    }
    
    [Test]
    public async Task MultipleContinuousTimelines_WithEqualState_ResultsIn_SingleTimeline_WithEqualStateRemoved()
    {
        var items1 = new TimelineItem<int>[]
        {
            new(0, 1),
            new(1, 2),
        };
        var timeline1 = StateTimeline.Create(2, StateKind, items1);
        
        var items2 = new TimelineItem<int>[]
        {
            new(2, 2),
            new(3, 4),
        };
        var timeline2 = StateTimeline.Create(4, StateKind, items2);

        var input = new[]
        {
            timeline2,
            timeline1,
        };
        
        var result = StateTimeline.Merge(input);

        const double expectedDuration = 4;
        var expectedItems = new TimelineItem<int>[]
        {
            new(0, 1),
            new(1, 2),
            new(3, 4),
        };

        await Check.That(result)
            .IsNotNull()
            .And.Satisfies(x => x.IsEmpty, assert => assert.IsFalse())
            .And.Satisfies(x => x.Duration, assert => assert.IsEqualTo(expectedDuration))
            .And.Satisfies(x => x.Count, assert => assert.IsEqualTo(expectedItems.Length))
            .And.Satisfies(x => x.AsEnumerable(), assert => assert.IsEquivalentTo(expectedItems));
    }
    
    [Test]
    public async Task MultipleSeparatedTimelines_WithEqualState_ResultsIn_SingleTimeline_WithDefaultStateAdded()
    {
        var items1 = new TimelineItem<int>[]
        {
            new(0, 1),
            new(0.5, 2),
        };
        var timeline1 = StateTimeline.Create(1, StateKind, items1);
        
        var items2 = new TimelineItem<int>[]
        {
            new(2, 2),
            new(3, 4),
        };
        var timeline2 = StateTimeline.Create(4, StateKind, items2);

        var input = new[]
        {
            timeline2,
            timeline1,
        };
        
        var result = StateTimeline.Merge(input);

        const double expectedDuration = 4;
        var expectedItems = new TimelineItem<int>[]
        {
            new(0, 1),
            new(0.5, 2),
            new(1, 0),
            new(2, 2),
            new(3, 4),
        };

        await Check.That(result)
            .IsNotNull()
            .And.Satisfies(x => x.IsEmpty, assert => assert.IsFalse())
            .And.Satisfies(x => x.Duration, assert => assert.IsEqualTo(expectedDuration))
            .And.Satisfies(x => x.Count, assert => assert.IsEqualTo(expectedItems.Length))
            .And.Satisfies(x => x.AsEnumerable(), assert => assert.IsEquivalentTo(expectedItems));
    }
    
    [Test]
    public async Task MultipleSamePositionTimelines_ResultsIn_MergedStateTimeline()
    {
        var items1 = new TimelineItem<int>[]
        {
            new(0, 1),
            new(1, 2),
        };
        var timeline1 = StateTimeline.Create(2, StateKind, items1);
        
        var items2 = new TimelineItem<int>[]
        {
            new(1, 3),
            new(2, 4),
        };
        var timeline2 = StateTimeline.Create(4, StateKind, items2);

        var input = new[]
        {
            timeline2,
            timeline1,
        };
        
        var result = StateTimeline.Merge(input);

        const double expectedDuration = 4;
        var expectedItems = new TimelineItem<int>[]
        {
            new(0, 1),
            new(1, 5),
            new(2, 4),
        };

        await Check.That(result)
            .IsNotNull()
            .And.Satisfies(x => x.IsEmpty, assert => assert.IsFalse())
            .And.Satisfies(x => x.Duration, assert => assert.IsEqualTo(expectedDuration))
            .And.Satisfies(x => x.Count, assert => assert.IsEqualTo(expectedItems.Length))
            .And.Satisfies(x => x.AsEnumerable(), assert => assert.IsEquivalentTo(expectedItems));
    }
    
    [Test]
    public async Task MultipleSamePositionTimelines_WithAggregatedDefaultState_ResultsIn_MergedStateTimeline_WithNoDefaultState()
    {
        var items1 = new TimelineItem<int>[]
        {
            new(0, 1),
            new(1, 2),
        };
        var timeline1 = StateTimeline.Create(2, StateKind, items1);
        
        var items2 = new TimelineItem<int>[]
        {
            new(0, -1),
            new(2, 4),
        };
        var timeline2 = StateTimeline.Create(4, StateKind, items2);

        var input = new[]
        {
            timeline2,
            timeline1,
        };
        
        var result = StateTimeline.Merge(input);

        const double expectedDuration = 4;
        var expectedItems = new TimelineItem<int>[]
        {
            new(1, 1),
            new(2, 4),
        };

        await Check.That(result)
            .IsNotNull()
            .And.Satisfies(x => x.IsEmpty, assert => assert.IsFalse())
            .And.Satisfies(x => x.Duration, assert => assert.IsEqualTo(expectedDuration))
            .And.Satisfies(x => x.Count, assert => assert.IsEqualTo(expectedItems.Length))
            .And.Satisfies(x => x.AsEnumerable(), assert => assert.IsEquivalentTo(expectedItems));
    }
    
    [Test]
    public async Task MultipleSamePositionTimelines_WithAggregatedEqualState_ResultsIn_MergedStateTimeline_WithNoEqualState()
    {
        var items1 = new TimelineItem<int>[]
        {
            new(0, 1),
            new(1, 2),
            new(3, 0),
        };
        var timeline1 = StateTimeline.Create(4, StateKind, items1);
        
        var items2 = new TimelineItem<int>[]
        {
            new(0.5, 1),
            new(1, 0),
            new(2, -1),
        };
        var timeline2 = StateTimeline.Create(4, StateKind, items2);

        var input = new[]
        {
            timeline2,
            timeline1,
        };
        
        var result = StateTimeline.Merge(input);

        const double expectedDuration = 4;
        var expectedItems = new TimelineItem<int>[]
        {
            new(0, 1),
            new(0.5, 2),
            new(2, 1),
            new(3, -1),
        };

        await Check.That(result)
            .IsNotNull()
            .And.Satisfies(x => x.IsEmpty, assert => assert.IsFalse())
            .And.Satisfies(x => x.Duration, assert => assert.IsEqualTo(expectedDuration))
            .And.Satisfies(x => x.Count, assert => assert.IsEqualTo(expectedItems.Length))
            .And.Satisfies(x => x.AsEnumerable(), assert => assert.IsEquivalentTo(expectedItems));
    }
    
    [Test]
    public async Task MultipleSamePositionTimelines_WithAggregatedDefaultState_ResultsIn_Empty()
    {
        var items1 = new TimelineItem<int>[]
        {
            new(0, 1),
            new(1, 2),
        };
        var timeline1 = StateTimeline.Create(2, StateKind, items1);
        
        var items2 = new TimelineItem<int>[]
        {
            new(0, -1),
            new(1, -2),
            new(2, 0),
        };
        var timeline2 = StateTimeline.Create(4, StateKind, items2);

        var input = new[]
        {
            timeline2,
            timeline1,
        };
        
        var result = StateTimeline.Merge(input);

        await Check.That(result)
            .IsNotNull()
            .And.Satisfies(x => x.IsEmpty, assert => assert.IsTrue())
            .And.Satisfies(x => x.Duration, assert => assert.IsZero())
            .And.Satisfies(x => x.Count, assert => assert.IsZero())
            .And.Satisfies(x => !(x.AsEnumerable()).Any(), assert => assert.IsTrue());
    }
}
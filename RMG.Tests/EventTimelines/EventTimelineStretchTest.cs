using Rmg.Core.Events;
using TUnit.Assertions.AssertConditions.Throws;

namespace Rmg.Tests.EventTimelines;

public sealed class EventTimelineStretchTest
{
    [Test]
    [Arguments(0)]
    [Arguments(1)]
    [Arguments(2)]
    public async Task Empty_Stretches_ToEmpty(double offset)
    {
        var input = EventTimeline.Empty<int>();
        
        var result = input.Stretch(offset);

        await Assert.That(result)
            .IsNotNull()
            .And.Satisfies(x => x.IsEmpty, assert => assert.IsTrue())
            .And.Satisfies(x => x.Duration, assert => assert.IsZero())
            .And.Satisfies(x => x.Count, assert => assert.IsZero())
            .And.Satisfies(x => x.AsEnumerable(), assert => assert.IsEmpty());
    }
    
    [Test]
    public async Task ZeroFactorStretch_ToEmpty()
    {
        const double duration = 1;
        var items = new TimelineItem<int>[]
        {
            new(0, 1),
            new(1, 2),
        };
        
        var input = EventTimeline.Create(duration, items);
        
        var result = input.Stretch(0);

        await Assert.That(result)
            .IsNotNull()
            .And.Satisfies(x => x.IsEmpty, assert => assert.IsTrue())
            .And.Satisfies(x => x.Duration, assert => assert.IsZero())
            .And.Satisfies(x => x.Count, assert => assert.IsZero())
            .And.Satisfies(x => x.AsEnumerable(), assert => assert.IsEmpty());
    }
    
    [Test]
    public async Task NegativeFactorStretch_ThrowsException()
    {
        var input = EventTimeline.Empty<int>();
        
        await Assert.That(() =>
        {
            input.Stretch(-1);
        }).Throws<ArgumentOutOfRangeException>();
    }
    
    [Test]
    public async Task OneFactorStretch_SameItems()
    {
        const double duration = 2;
        var items = new TimelineItem<int>[]
        {
            new(0, 1),
            new(1, 2),
        };
        
        var input = EventTimeline.Create(duration, items);
        
        var result = input.Stretch(1);

        await Assert.That(result)
            .IsNotNull()
            .And.Satisfies(x => x.IsEmpty, assert => assert.IsFalse())
            .And.Satisfies(x => x.Duration, assert => assert.IsEqualTo(duration))
            .And.Satisfies(x => x.Count, assert => assert.IsEqualTo(items.Length))
            .And.Satisfies(x => x.AsEnumerable(), assert => assert.IsEquivalentTo(items));
    }
    
    [Test]
    public async Task MoreThenOneFactorStretch_StretchesItems()
    {
        const double duration = 2;
        var items = new TimelineItem<int>[]
        {
            new(0, 1),
            new(1, 2),
        };
        
        var input = EventTimeline.Create(duration, items);
        
        var result = input.Stretch(2);

        const double expectedDuration = 4;
        var expectedItems = new TimelineItem<int>[]
        {
            new(0, 1),
            new(2, 2),
        };

        await Assert.That(result)
            .IsNotNull()
            .And.Satisfies(x => x.IsEmpty, assert => assert.IsFalse())
            .And.Satisfies(x => x.Duration, assert => assert.IsEqualTo(expectedDuration))
            .And.Satisfies(x => x.Count, assert => assert.IsEqualTo(expectedItems.Length))
            .And.Satisfies(x => x.AsEnumerable(), assert => assert.IsEquivalentTo(expectedItems));
    }
    
    [Test]
    public async Task LessThenOneFactorStretch_StretchesItems()
    {
        const double duration = 2;
        var items = new TimelineItem<int>[]
        {
            new(0, 1),
            new(1, 2),
        };
        
        var input = EventTimeline.Create(duration, items);
        
        var result = input.Stretch(0.5);

        const double expectedDuration = 1;
        var expectedItems = new TimelineItem<int>[]
        {
            new(0, 1),
            new(0.5, 2),
        };

        await Assert.That(result)
            .IsNotNull()
            .And.Satisfies(x => x.IsEmpty, assert => assert.IsFalse())
            .And.Satisfies(x => x.Duration, assert => assert.IsEqualTo(expectedDuration))
            .And.Satisfies(x => x.Count, assert => assert.IsEqualTo(expectedItems.Length))
            .And.Satisfies(x => x.AsEnumerable(), assert => assert.IsEquivalentTo(expectedItems));
    }
}
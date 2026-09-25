using Rmg.Core.Events;

namespace Rmg.Tests.EventTimelines;

public sealed class EventTimelineStretchTest
{
    [Test]
    [Arguments(0)]
    [Arguments(1)]
    [Arguments(2)]
    public async Task ZeroDurationTimeline_Stretches_ToZeroDuration(double offset)
    {
        var input = EventTimeline.Create<int>(0);
        
        var result = input.Stretch(offset);

        await Check.That(result)
            .IsNotNull()
            .And.Satisfies(x => x.Duration, assert => assert.IsZero())
            .And.Satisfies(x => x.Count, assert => assert.IsZero())
            .And.Satisfies(x => !(x.AsEnumerable()).Any(), assert => assert.IsTrue());
    }
    
    [Test]
    public async Task ZeroFactorStretch_ToZeroDuration()
    {
        const double duration = 1;
        var items = new TimelineItem<int>[]
        {
            new(0, 1),
            new(1, 2),
        };
        
        var input = EventTimeline.Create(duration, items);
        
        var result = input.Stretch(0);

        await Check.That(result)
            .IsNotNull()
            .And.Satisfies(x => x.Duration, assert => assert.IsZero())
            .And.Satisfies(x => x.Count, assert => assert.IsZero())
            .And.Satisfies(x => !(x.AsEnumerable()).Any(), assert => assert.IsTrue());
    }
    
    [Test]
    public async Task NegativeFactorStretch_ThrowsException()
    {
        var input = EventTimeline.Create<int>(0);
        
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

        await Check.That(result)
            .IsNotNull()
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

        await Check.That(result)
            .IsNotNull()
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

        await Check.That(result)
            .IsNotNull()
            .And.Satisfies(x => x.Duration, assert => assert.IsEqualTo(expectedDuration))
            .And.Satisfies(x => x.Count, assert => assert.IsEqualTo(expectedItems.Length))
            .And.Satisfies(x => x.AsEnumerable(), assert => assert.IsEquivalentTo(expectedItems));
    }
}
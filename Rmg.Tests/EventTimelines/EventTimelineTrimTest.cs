using Rmg.Core.Events;

namespace Rmg.Tests.EventTimelines;

public sealed class EventTimelineTrimTest
{
    [Test]
    public async Task Empty_Trims_ToEmpty()
    {
        var input = EventTimeline.Empty<int>();
        
        var result = input.Trim(1);

        await Check.That(result)
            .IsNotNull()
            .And.Satisfies(x => x.IsEmpty, assert => assert.IsTrue())
            .And.Satisfies(x => x.Duration, assert => assert.IsZero())
            .And.Satisfies(x => x.Count, assert => assert.IsZero())
            .And.Satisfies(x => !(x.AsEnumerable()).Any(), assert => assert.IsTrue());
    }
    
    [Test]
    public async Task ZeroDuration_Trim_ToEmpty()
    {
        const double duration = 1;
        var items = new TimelineItem<int>[]
        {
            new(0, 1)
        };
        
        var input = EventTimeline.Create(duration, items);
        
        var result = input.Trim(0);

        await Check.That(result)
            .IsNotNull()
            .And.Satisfies(x => x.IsEmpty, assert => assert.IsTrue())
            .And.Satisfies(x => x.Duration, assert => assert.IsZero())
            .And.Satisfies(x => x.Count, assert => assert.IsZero())
            .And.Satisfies(x => !(x.AsEnumerable()).Any(), assert => assert.IsTrue());
    }
    
    [Test]
    public async Task BeforeFirstItemDuration_Trim_ToEmpty()
    {
        const double duration = 2;
        var items = new TimelineItem<int>[]
        {
            new(1, 1)
        };
        
        var input = EventTimeline.Create(duration, items);
        
        var result = input.Trim(1);

        await Check.That(result)
            .IsNotNull()
            .And.Satisfies(x => x.IsEmpty, assert => assert.IsTrue())
            .And.Satisfies(x => x.Duration, assert => assert.IsZero())
            .And.Satisfies(x => x.Count, assert => assert.IsZero())
            .And.Satisfies(x => !(x.AsEnumerable()).Any(), assert => assert.IsTrue());
    }

    [Test]
    public async Task NegativeDuration_Trim_ThrowsException()
    {
        var input = EventTimeline.Empty<int>();

        await Assert.That(() =>
        {
            input.Trim(-1);
        }).Throws<ArgumentOutOfRangeException>();
    }
    
    [Test]
    public async Task SameDuration_Trim_ToSameItems()
    {
        const double duration = 1;
        var items = new TimelineItem<int>[]
        {
            new(0, 1)
        };
        
        var input = EventTimeline.Create(duration, items);
        
        var result = input.Trim(duration);

        await Check.That(result)
            .IsNotNull()
            .And.Satisfies(x => x.IsEmpty, assert => assert.IsFalse())
            .And.Satisfies(x => x.Duration, assert => assert.IsEqualTo(duration))
            .And.Satisfies(x => x.Count, assert => assert.IsEqualTo(items.Length))
            .And.Satisfies(x => x.AsEnumerable(), assert => assert.IsEquivalentTo(items));
    }
    
    [Test]
    public async Task LargerDuration_Trim_ToSameItems()
    {
        const double duration = 1;
        var items = new TimelineItem<int>[]
        {
            new(0, 1)
        };
        
        var input = EventTimeline.Create(duration, items);
        
        const double newDuration = 2;
        var result = input.Trim(newDuration);

        await Check.That(result)
            .IsNotNull()
            .And.Satisfies(x => x.IsEmpty, assert => assert.IsFalse())
            .And.Satisfies(x => x.Duration, assert => assert.IsEqualTo(newDuration))
            .And.Satisfies(x => x.Count, assert => assert.IsEqualTo(items.Length))
            .And.Satisfies(x => x.AsEnumerable(), assert => assert.IsEquivalentTo(items));
    }
    
    [Test]
    public async Task PartialLowerDuration_Trim_TrimsItems()
    {
        const double duration = 2;
        var items = new TimelineItem<int>[]
        {
            new(0, 1),
            new(1, 2),
        };
        
        var input = EventTimeline.Create(duration, items);
        
        const double newDuration = 1;
        var result = input.Trim(newDuration);
        
        var expectedItems = new TimelineItem<int>[]
        {
            new(0, 1)
        };

        await Check.That(result)
            .IsNotNull()
            .And.Satisfies(x => x.IsEmpty, assert => assert.IsFalse())
            .And.Satisfies(x => x.Duration, assert => assert.IsEqualTo(newDuration))
            .And.Satisfies(x => x.Count, assert => assert.IsEqualTo(expectedItems.Length))
            .And.Satisfies(x => x.AsEnumerable(), assert => assert.IsEquivalentTo(expectedItems));
    }
}
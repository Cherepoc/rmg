using Rmg.Core.Events;

namespace Rmg.Tests.EventTimelines;

public sealed class EventTimelineMapValuesTest
{
    [Test]
    public async Task ZeroDurationTimeline_Maps_ToZeroDuration()
    {
        var input = EventTimeline.Create<int>(0);
        
        Func<int, double> mapFunc = x => x * 0.5;
        
        var result = input.MapValues(mapFunc);

        await Check.That(result)
            .IsNotNull()
            .And.Satisfies(x => x.Duration, assert => assert.IsZero())
            .And.Satisfies(x => x.Count, assert => assert.IsZero())
            .And.Satisfies(x => !(x.AsEnumerable()).Any(), assert => assert.IsTrue());
    }
    
    [Test]
    public async Task WithEvents_Maps_ToNewItems()
    {
        const double duration = 2;
        var items = new TimelineItem<int>[]
        {
            new(0, 1),
            new(1, 2),
        };
        
        var input = EventTimeline.Create(duration, items);
        
        Func<int, double> mapFunc = x => x * 0.5;
        
        var result = input.MapValues(mapFunc);
        
        var expectedItems = new TimelineItem<double>[]
        {
            new(0, 0.5),
            new(1, 1),
        };

        await Check.That(result)
            .IsNotNull()
            .And.Satisfies(x => x.Duration, assert => assert.IsEqualTo(duration))
            .And.Satisfies(x => x.Count, assert => assert.IsEqualTo(expectedItems.Length))
            .And.Satisfies(x => x.AsEnumerable(), assert => assert.IsEquivalentTo(expectedItems));
    }
}
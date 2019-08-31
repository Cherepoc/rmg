using System.Collections.Generic;
using RMG.Core.Music;
using RMG.Core.Render;
using Xunit;

namespace RMG.Tests.EventTimelineOperationsTests
{
    public class SubTimelineTests
    {
        private static TimedEvent<int> CreateTimedEvent(int value, double position)
        {
            return new TimedEvent<int>
            {
                Event = value,
                Position = position
            };
        }

        [Fact]
        public void TestEmpty()
        {
            var timeline = new List<TimedEvent<int>>();

            var actual = EventTimelineOperations.SubTimeline(timeline, 1, 5);

            var expected = new List<TimedEvent<int>>();

            Assert.Equal(expected, actual);
        }

        [Fact]
        public void TestIntervalInwards()
        {
            var timeline = new List<TimedEvent<int>>
            {
                CreateTimedEvent(1, 0),
                CreateTimedEvent(2, 1),
                CreateTimedEvent(3, 2),
                CreateTimedEvent(4, 3)
            };

            var actual = EventTimelineOperations.SubTimeline(timeline, 1, 2);

            var expected = new List<TimedEvent<int>>
            {
                CreateTimedEvent(2, 0),
                CreateTimedEvent(3, 1)
            };

            Assert.Equal(expected, actual);
        }

        [Fact]
        public void TestIntervalOutwards()
        {
            var timeline = new List<TimedEvent<int>>
            {
                CreateTimedEvent(1, 2),
                CreateTimedEvent(2, 3),
                CreateTimedEvent(3, 4),
                CreateTimedEvent(4, 5)
            };

            var actual = EventTimelineOperations.SubTimeline(timeline, 1, 5);

            var expected = new List<TimedEvent<int>>
            {
                CreateTimedEvent(1, 1),
                CreateTimedEvent(2, 2),
                CreateTimedEvent(3, 3),
                CreateTimedEvent(4, 4)
            };

            Assert.Equal(expected, actual);
        }
    }
}

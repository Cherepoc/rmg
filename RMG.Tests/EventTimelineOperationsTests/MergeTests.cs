using System.Collections.Generic;
using RMG.Core.Music;
using RMG.Core.Render;
using Xunit;

namespace RMG.Tests.EventTimelineOperationsTests
{
    public class MergeTests
    {
        private static int IntMergeFunction(int a, int b)
        {
            return a + b;
        }

        private static TimedEvent<int> CreateTimedEvent(double position, int value)
        {
            return new TimedEvent<int>
            {
                Event = value,
                Position = position
            };
        }

        [Fact]
        public void TestBothEmpty()
        {
            var source = new List<TimedEvent<int>>();
            var target = new List<TimedEvent<int>>();

            var actual = EventTimelineOperations.Merge(source, target, 0, 1, IntMergeFunction, 0);

            Assert.Empty(actual);
        }

        [Fact]
        public void TestPositionSplit()
        {
            var source = new List<TimedEvent<int>>
            {
                CreateTimedEvent(0, 1),
                CreateTimedEvent(1, 2),
                CreateTimedEvent(2, 1)
            };
            var target = new List<TimedEvent<int>>
            {
                CreateTimedEvent(0.5, 1),
                CreateTimedEvent(1.5, 0),
                CreateTimedEvent(2.5, 1)
            };

            var actual = EventTimelineOperations.Merge(source, target, 1, 2, IntMergeFunction, 0);

            var expectedSource = new List<TimedEvent<int>>
            {
                CreateTimedEvent(0, 1),
                CreateTimedEvent(1, 2),
                CreateTimedEvent(1.5, 3),
                CreateTimedEvent(2, 2),
                CreateTimedEvent(2.5, 1),
                CreateTimedEvent(3, 1)
            };

            Assert.Equal(expectedSource, actual);
        }

        [Fact]
        public void TestPrecedingTargetSplit()
        {
            var source = new List<TimedEvent<int>>
            {
                CreateTimedEvent(2, 1),
                CreateTimedEvent(3, 2),
                CreateTimedEvent(4, 1)
            };
            var target = new List<TimedEvent<int>>
            {
                CreateTimedEvent(0.5, 1),
                CreateTimedEvent(1.5, 0),
                CreateTimedEvent(2.5, 1)
            };

            var actual = EventTimelineOperations.Merge(source, target, 0, 2, IntMergeFunction, 0);

            var expectedSource = new List<TimedEvent<int>>
            {
                CreateTimedEvent(0.5, 1),
                CreateTimedEvent(1.5, 0),
                CreateTimedEvent(2, 1),
                CreateTimedEvent(3, 2),
                CreateTimedEvent(4, 1)
            };

            Assert.Equal(expectedSource, actual);
        }

        [Fact]
        public void TestSimpleSplit()
        {
            var source = new List<TimedEvent<int>>
            {
                CreateTimedEvent(0, 1),
                CreateTimedEvent(1, 1)
            };
            var target = new List<TimedEvent<int>>
            {
                CreateTimedEvent(0.5, 1),
                CreateTimedEvent(1.5, 0),
                CreateTimedEvent(2.5, 1)
            };

            var actual = EventTimelineOperations.Merge(source, target, 0, 2, IntMergeFunction, 0);

            var expectedSource = new List<TimedEvent<int>>
            {
                CreateTimedEvent(0, 1),
                CreateTimedEvent(0.5, 2),
                CreateTimedEvent(1, 2),
                CreateTimedEvent(1.5, 1),
                CreateTimedEvent(2, 1)
            };

            Assert.Equal(expectedSource, actual);
        }

        [Fact]
        public void TestSourceEmpty()
        {
            var source = new List<TimedEvent<int>>();
            var target = new List<TimedEvent<int>>
            {
                CreateTimedEvent(0, 1)
            };

            var actual = EventTimelineOperations.Merge(source, target, 0, 1, IntMergeFunction, 0);

            var expectedSource = new List<TimedEvent<int>>
            {
                CreateTimedEvent(0, 1),
                CreateTimedEvent(1, 0)
            };

            Assert.Equal(expectedSource, actual);
        }

        [Fact]
        public void TestTargetEmpty()
        {
            var source = new List<TimedEvent<int>>
            {
                CreateTimedEvent(0, 1)
            };
            var target = new List<TimedEvent<int>>();

            var actual = EventTimelineOperations.Merge(source, target, 0, 1, IntMergeFunction, 0);

            var expectedSource = new List<TimedEvent<int>>
            {
                CreateTimedEvent(0, 1)
            };

            Assert.Equal(expectedSource, actual);
        }
    }
}

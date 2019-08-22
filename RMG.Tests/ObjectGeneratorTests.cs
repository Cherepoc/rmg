using System;
using System.Collections.Generic;
using RMG.Core.Generation;
using RMG.Core.Music;
using Xunit;

namespace RMG.Tests
{
    public sealed class ObjectGeneratorTests
    {
        private static readonly GenerationContext GenerationContext = new GenerationContext(new Random(0));

        private static CollectionGenerator<int> CreateSourceGenerator()
        {
            return new CollectionGenerator<int>
            {
                ItemCountGenerator = new ConstantGenerator<int>(4),
                ItemGenerator = new ConstantGenerator<int>(0)
            };
        }

        private static LinkedEntityCollectionGenerator<TestClass, int> CreateTargetGenerator()
        {
            return new LinkedEntityCollectionGenerator<TestClass, int>
            {
                ItemCountGenerator = new ConstantGenerator<int>(4),
                LinkedCollectionAccessor = x => x.Source
            };
        }

        private sealed class TestClass : IDuration
        {
            public IList<int> Source { get; set; } = new List<int>();
            public IList<int> Target { get; set; } = new List<int>();
            public IList<TimedEvent<double>> Timeline { get; set; }
            public double Duration { get; set; }
            
            public NoteBasePattern NoteBasePattern { get; set; }
        }

        [Fact]
        public void TestDependantOrder()
        {
            var generator = new ObjectGenerator<TestClass>()
                .WithPropertyGenerator(x => x.Target, CreateTargetGenerator(), x => x.Source)
                .WithPropertyGenerator(x => x.Source, CreateSourceGenerator());

            var generatedObject = generator.Generate(GenerationContext);

            Assert.Equal(new List<int> {0, 0, 0, 0}, generatedObject.Target);
        }

        [Fact]
        public void TestRightOrder()
        {
            var generator = new ObjectGenerator<TestClass>()
                .WithPropertyGenerator(x => x.Source, CreateSourceGenerator())
                .WithPropertyGenerator(x => x.Target, CreateTargetGenerator());

            var generatedObject = generator.Generate(GenerationContext);

            Assert.Equal(new List<int> {0, 0, 0, 0}, generatedObject.Target);
        }

        [Fact]
        public void TestTimelineDependantOrder()
        {
            var generator = new ObjectGenerator<TestClass>()
                .WithPropertyGenerator(
                    x => x.Timeline,
                    new TimedEventGenerator<double>
                    {
                        EventGenerator = new LinkedEntityGenerator<TestClass, double>
                        {
                            LinkedCollectionAccessor = x => new List<double>{x.Duration}
                        },
                        Offset = 0,
                        Scale = 1,
                        MaxRank = 0,
                        RankProbabilityFunction = new RankProbabilityFunction
                        {
                            Min = 0,
                            Max = 1,
                            Multiplier = 1
                        }
                    })
                .WithPropertyGenerator(x => x.Duration, new ConstantGenerator<double>(4));

            var generatedObject = generator.Generate(GenerationContext);

            var expectedTimeline = new List<TimedEvent<double>> {new TimedEvent<double>(0, 4)};
            Assert.Equal(expectedTimeline, generatedObject.Timeline);
        }

        [Fact]
        public void TestChildDurationGeneration()
        {
            var generator = new ObjectGenerator<TestClass>()
                .WithPropertyGenerator(
                    x => x.NoteBasePattern,
                    new ObjectGenerator<NoteBasePattern>())
                .WithPropertyGenerator(x => x.Duration, new ConstantGenerator<double>(4));

            var generatedObject = generator.Generate(GenerationContext);

            Assert.Equal(4, generatedObject.NoteBasePattern.Duration);
        }

        [Fact]
        public void TestWrongOrder()
        {
            var generator = new ObjectGenerator<TestClass>()
                .WithPropertyGenerator(x => x.Target, CreateTargetGenerator())
                .WithPropertyGenerator(x => x.Source, CreateSourceGenerator());

            var generatedObject = generator.Generate(GenerationContext);

            Assert.Equal(new List<int>(), generatedObject.Target);
        }
    }
}

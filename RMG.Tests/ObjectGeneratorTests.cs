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

        private static LinkedEntityCollectionGenerator<int> CreateTargetGenerator()
        {
            return new LinkedEntityCollectionGenerator<int>()
                .LinkEntityCollection(targetLink => targetLink.FromParentProperty<TestClass>(x => x.Source))
                .WithItemCountGenerator(new ConstantGenerator<int>(4));
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
                .WithProperty(
                    x => x.Target,
                    property => property
                        .WithGenerator(CreateTargetGenerator())
                        .DependsOn(x => x.Source))
                .WithProperty(
                    x => x.Source,
                    property => property.WithGenerator(CreateSourceGenerator()));

            var generatedObject = generator.Generate(GenerationContext);

            Assert.Equal(new List<int> {0, 0, 0, 0}, generatedObject.Target);
        }

        [Fact]
        public void TestRightOrder()
        {
            var generator = new ObjectGenerator<TestClass>()
                    .WithProperty(
                        x => x.Source,
                        property => property.WithGenerator(CreateSourceGenerator()))
                    .WithProperty(
                        x => x.Target,
                        property => property.WithGenerator(CreateTargetGenerator()));

            var generatedObject = generator.Generate(GenerationContext);

            Assert.Equal(new List<int> {0, 0, 0, 0}, generatedObject.Target);
        }

        [Fact]
        public void TestTimelineDependantOrder()
        {
            var generator = new ObjectGenerator<TestClass>()
                .WithProperty(
                    x => x.Timeline,
                    property => property.WithGenerator(new TimedEventGenerator<double>
                    {
                        EventGenerator = new LinkedEntityGenerator<TestClass, double>
                        {
                            LinkedCollectionAccessor = x => new List<double>{x.Duration}
                        },
                        Offset = 0,
                        Scale = 4,
                        MaxRank = 0,
                        RankProbabilityFunction = new RankProbabilityFunction
                        {
                            Min = 0,
                            Max = 1,
                            Multiplier = 1
                        }
                    }))
                .WithProperty(
                    x => x.Duration,
                    property => property.WithValue(4));

            var generatedObject = generator.Generate(GenerationContext);

            var expectedTimeline = new List<TimedEvent<double>> {new TimedEvent<double>(0, 4)};
            Assert.Equal(expectedTimeline, generatedObject.Timeline);
        }

        [Fact]
        public void TestChildDurationGeneration()
        {
            var generator = new ObjectGenerator<TestClass>()
                .WithProperty(
                    x => x.NoteBasePattern,
                    property => property.WithGenerator(new ObjectGenerator<NoteBasePattern>()))
                .WithProperty(
                    x => x.Duration,
                    property => property.WithValue(4));

            var generatedObject = generator.Generate(GenerationContext);

            Assert.Equal(4, generatedObject.NoteBasePattern.Duration);
        }

        [Fact]
        public void TestWrongOrder()
        {
            var generator = new ObjectGenerator<TestClass>()
                .WithProperty(
                    x => x.Target,
                    property => property.WithGenerator(CreateTargetGenerator()))
                .WithProperty(
                    x => x.Source,
                    property => property.WithGenerator(CreateSourceGenerator()));

            var generatedObject = generator.Generate(GenerationContext);

            Assert.Equal(new List<int>(), generatedObject.Target);
        }
    }
}

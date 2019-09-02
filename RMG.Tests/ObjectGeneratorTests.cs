using System;
using System.Collections.Generic;
using RMG.Core.Generation;
using RMG.Core.Generation.ContextGenerators;
using RMG.Core.Generation.ObjectGenerators;
using RMG.Core.Generation.RandomGenerators;
using RMG.Core.Music;
using RMG.Core.ProbabilityCalculation;
using Xunit;

namespace RMG.Tests
{
    public sealed class ObjectGeneratorTests
    {
        private static readonly GenerationContext GenerationContext = new GenerationContext(new Random(0));

        private static IntGenerator CreateSourceGenerator()
        {
            return new IntGenerator(1, 2);
        }

        private static ContextEntityPropertyGenerator<TestClass, int> CreateTargetGenerator()
        {
            return new ContextEntityPropertyGenerator<TestClass, int>().FromProperty(x => x.Source);
        }

        private sealed class TestClass : IDuration
        {
            public int Source { get; set; }
            public int Target { get; set; }
            public IList<TimedEvent<double>> Timeline { get; set; }

            public NoteBasePattern NoteBasePattern { get; set; }
            public double Duration { get; set; }
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

            Assert.Equal(1, generatedObject.Target);
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

            Assert.Equal(1, generatedObject.Target);
        }

        [Fact]
        public void TestTimelineDependantOrder()
        {
            var generator = new ObjectGenerator<TestClass>()
                .WithProperty(
                    x => x.Timeline,
                    property => property.WithGenerator(
                        new RhythmTimelineGenerator<double>
                        {
                            EventGenerator = new LinkedEntityGenerator<TestClass, double>
                            {
                                LinkedCollectionAccessor = x => new List<double> {x.Duration}
                            },
                            OffsetGenerator = new ConstantGenerator<double>(0),
                            ScaleGenerator = new ConstantGenerator<double>(4),
                            MaxPowerGenerator = new ConstantGenerator<int>(0),
                            ProbabilityFunctionGenerator = new ConstantGenerator<GeometricProbabilityFunction>(
                                new GeometricProbabilityFunction
                                {
                                    MinProbability = 0,
                                    MaxProbability = 1,
                                    ProbabilityMultiplier = 1
                                })
                        }))
                .WithProperty(
                    x => x.Duration,
                    property => property.WithValue(4));

            var generatedObject = generator.Generate(GenerationContext);

            var expectedTimeline = new List<TimedEvent<double>> {new TimedEvent<double>(0, 4)};
            Assert.Equal(expectedTimeline, generatedObject.Timeline);
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

            Assert.Equal(0, generatedObject.Target);
        }
    }
}

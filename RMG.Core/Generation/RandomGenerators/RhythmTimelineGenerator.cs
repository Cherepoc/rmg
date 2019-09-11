using System;
using System.Collections.Generic;
using System.Linq;
using RMG.Core.Music;
using RMG.Core.ProbabilityCalculation;
using RMG.Core.Utils;

namespace RMG.Core.Generation.RandomGenerators
{
    public sealed class RhythmTimelineGenerator<T> : GeneratorBase<IEnumerable<TimedEvent<T>>>
    {
        public IGenerator<T> EventGenerator { get; set; }

        public IGenerator<IIntProbabilityFunction> ProbabilityFunctionGenerator { get; set; }

        public IGenerator<int> MaxPowerGenerator { get; set; }

        public IGenerator<double> OffsetGenerator { get; set; }

        public IGenerator<double> PeriodGenerator { get; set; }

        public override IEnumerable<TimedEvent<T>> Generate(GenerationContext context)
        {
            var parent = context.FindParentValue<IDuration>();
            if (parent == null)
            {
                throw new ApplicationException(
                    $"Cannot use {nameof(RhythmTimelineGenerator<T>)} outside of {nameof(IDuration)}");
            }

            var duration = parent.Duration;

            if (duration <= 0)
            {
                return Array.Empty<TimedEvent<T>>();
            }

            var (probabilityFunction, maxPower, offset, scale) = context.Generate(
                ProbabilityFunctionGenerator,
                MaxPowerGenerator,
                OffsetGenerator,
                PeriodGenerator);

            var timelineContext = new TimelineGenerationContext(
                context,
                probabilityFunction,
                maxPower,
                offset,
                scale,
                duration);

            var result = new List<TimedEvent<T>>();
            var cycleCount = (int) Math.Ceiling(duration / timelineContext.Scale);
            for (var cycle = 0; cycle < cycleCount; cycle++)
            {
                var events = GenerateInternal(timelineContext, 0, 1, 0, cycle);
                result.AddRange(events.OrderBy(x => x.Position));
            }

            return result;
        }

        private IList<TimedEvent<T>> GenerateInternal(
            TimelineGenerationContext timelineContext,
            double normalizedPosition,
            double rankScale,
            int rank,
            int cycle
        )
        {
            var position = ConvertPosition(timelineContext, normalizedPosition, cycle);
            var probabilityFunction = timelineContext.GeometricProbabilityFunction;
            var result = new List<TimedEvent<T>>();
            var probability = probabilityFunction.GetProbability(rank);
            var testProbability = timelineContext.GenerationContext.Random.NextDouble();
            if (ProbabilityTester.TestProbability(testProbability, probability))
            {
                var generator = EventGenerator;
                var obj = generator.Generate(new RankedGenerationContext(timelineContext.GenerationContext, result, rank));
                result.Add(
                    new TimedEvent<T>
                    {
                        Event = obj,
                        Position = position
                    });
            }

            if (rank < timelineContext.MaxRank)
            {
                var childRank = rank + 1;
                var childRankScale = rankScale / 2;

                var leftPosition = normalizedPosition - childRankScale;
                if (TestPosition(timelineContext, leftPosition, cycle))
                {
                    var leftEvents = GenerateInternal(
                        timelineContext,
                        leftPosition,
                        childRankScale,
                        childRank,
                        cycle);
                    result.AddRange(leftEvents);
                }

                var rightPosition = normalizedPosition + childRankScale;
                if (TestPosition(timelineContext, rightPosition, cycle))
                {
                    var rightEvents = GenerateInternal(
                        timelineContext,
                        rightPosition,
                        childRankScale,
                        childRank,
                        cycle);
                    result.AddRange(rightEvents);
                }
            }

            return result;
        }

        private static double ConvertPosition(
            TimelineGenerationContext timelineContext,
            double normalizedPosition,
            int cycle
        )
        {
            return (normalizedPosition * timelineContext.Scale + timelineContext.Offset) % timelineContext.Scale
                   + cycle * timelineContext.Scale;
        }

        private static bool TestPosition(
            TimelineGenerationContext timelineContext,
            double normalizedPosition,
            int cycle
        )
        {
            var position = ConvertPosition(timelineContext, normalizedPosition, cycle);
            return position >= 0 && position < timelineContext.Duration;
        }

        private sealed class TimelineGenerationContext
        {
            public TimelineGenerationContext(
                GenerationContext generationContext,
                IIntProbabilityFunction geometricProbabilityFunction,
                int maxRank,
                double offset,
                double scale,
                double duration
            )
            {
                GenerationContext = generationContext;
                GeometricProbabilityFunction = geometricProbabilityFunction;
                MaxRank = maxRank;
                Offset = offset;
                Scale = scale;
                Duration = duration;
            }

            public GenerationContext GenerationContext { get; }

            public IIntProbabilityFunction GeometricProbabilityFunction { get; }

            public int MaxRank { get; }

            public double Offset { get; }

            public double Scale { get; }

            public double Duration { get; }
        }
    }
}

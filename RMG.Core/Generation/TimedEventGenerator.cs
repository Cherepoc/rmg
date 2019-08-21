using System;
using System.Collections.Generic;
using System.Linq;
using RMG.Core.Music;
using RMG.Core.Utils;

namespace RMG.Core.Generation
{
    public sealed class TimedEventGenerator<T> : IGenerator
    {
        public IGenerator EventGenerator { get; set; }

        public RankProbabilityFunction RankProbabilityFunction { get; set; }

        public int MaxRank { get; set; } = 5;

        public double Offset { get; set; } = 0;

        public double Scale { get; set; } = 1;

        public double Duration { get; set; } = 1;

        object IGenerator.Generate(GenerationContext context)
        {
            return Generate(context);
        }

        public IList<TimedEvent<T>> Generate(GenerationContext context)
        {
            if (Duration <= 0)
            {
                return Array.Empty<TimedEvent<T>>();
            }

            var result = new List<TimedEvent<T>>();
            var cycleCount = (int) Math.Ceiling(Duration / Scale);
            for (var cycle = 0; cycle < cycleCount; cycle++)
            {
                var events = GenerateInternal(context, 0, 1, 0, cycle);
                result.AddRange(events.OrderBy(x => x.Position));
            }

            return result;
        }

        private IList<TimedEvent<T>> GenerateInternal(
            GenerationContext context,
            double normalizedPosition,
            double rankScale,
            int rank,
            int cycle
        )
        {
            var position = ConvertPosition(normalizedPosition, cycle);
            var probabilityFunction = RankProbabilityFunction;
            var result = new List<TimedEvent<T>>();
            var probability = probabilityFunction.GetProbability(rank);
            if (context.Random.TestProbability(probability))
            {
                var generator = EventGenerator;
                var obj = (T) generator.Generate(new RankedGenerationContext(context, result, rank));
                result.Add(
                    new TimedEvent<T>
                    {
                        Event = obj,
                        Position = position
                    });
            }

            if (rank < MaxRank)
            {
                var childRank = rank + 1;
                var childRankScale = rankScale / 2;

                var leftPosition = normalizedPosition - childRankScale;
                if (TestPosition(leftPosition, cycle))
                {
                    var leftEvents = GenerateInternal(context, leftPosition, childRankScale, childRank, cycle);
                    result.AddRange(leftEvents);
                }

                var rightPosition = normalizedPosition + childRankScale;
                if (TestPosition(rightPosition, cycle))
                {
                    var rightEvents = GenerateInternal(context, rightPosition, childRankScale, childRank, cycle);
                    result.AddRange(rightEvents);
                }
            }

            return result;
        }

        private double ConvertPosition(double normalizedPosition, int cycle)
        {
            return (normalizedPosition * Scale + Offset) % Scale + cycle * Scale;
        }

        private bool TestPosition(double normalizedPosition, int cycle)
        {
            var position = ConvertPosition(normalizedPosition, cycle);
            return position >= 0 && position < Duration;
        }
    }
}

using System;
using RMG.Core.Music;

namespace RMG.Core.Generation
{
    public sealed class ScaleNoteOffsetGenerator : IGenerator
    {
        public double RankMultiplier { get; set; }

        object IGenerator.Generate(GenerationContext context)
        {
            return Generate(context);
        }

        public ScaleNoteOffset Generate(GenerationContext context)
        {
            var songContext = context.FindParent(c => c.Value is Song);
            var song = songContext.Value as Song;
            var scale = song.Scale;

            var rankProbabilities = GetRankProbabilities(scale.RankedOffsets.Count);

            var probability = context.Random.NextDouble();
            var rank = 0;
            var rankProbability = 0d;
            for (; rank < rankProbabilities.Length; rank++)
            {
                rankProbability = rankProbabilities[rank];
                if (probability < rankProbability)
                {
                    break;
                }

                probability -= rankProbability;
            }

            var offsets = scale.RankedOffsets[rank];
            var offset = (int) Math.Floor(probability / rankProbability * offsets.Count);
            return new ScaleNoteOffset
            {
                Rank = rank,
                Offset = offset
            };
        }

        private double[] GetRankProbabilities(int count)
        {
            var rankProbabilities = new double[count];
            var rankProbability = 1d;
            var rankProbabilitySum = 0d;
            for (var rank = 0; rank < rankProbabilities.Length; rank++)
            {
                rankProbabilities[rank] = rankProbability;
                rankProbabilitySum += rankProbability;
                rankProbability *= RankMultiplier;
            }

            for (var rank = 0; rank < rankProbabilities.Length; rank++)
            {
                rankProbabilities[rank] = rankProbabilities[rank] / rankProbabilitySum;
            }

            return rankProbabilities;
        }
    }
}

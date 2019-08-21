using RMG.Core.Music;
using RMG.Core.Utils;

namespace RMG.Core.Generation
{
    public sealed class ScaleNoteOffsetGenerator : IGenerator
    {
        public RankProbabilityFunction RankProbabilityFunction { get; set; }

        object IGenerator.Generate(GenerationContext context)
        {
            return Generate(context);
        }

        public int[] Generate(GenerationContext context)
        {
            var scaleOffset = new int[Scale.ScaleRankCount];
            for (var rank = 0; rank < scaleOffset.Length; rank++)
            {
                var rankProbability = RankProbabilityFunction.GetProbability(rank);
                if (context.Random.TestProbability(rankProbability))
                {
                    scaleOffset[rank] = context.Random.Next(-12 + 1, 12);
                }
            }

            return scaleOffset;
        }
    }
}

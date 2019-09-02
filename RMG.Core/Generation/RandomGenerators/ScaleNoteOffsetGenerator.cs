using RMG.Core.Music;
using RMG.Core.ProbabilityCalculation;
using RMG.Core.Utils;

namespace RMG.Core.Generation.RandomGenerators
{
    public sealed class ScaleNoteOffsetGenerator : GeneratorBase<int[]>
    {
        public IGenerator<IIntProbabilityFunction> ProbabilityFunctionGenerator { get; set; }

        public override int[] Generate(GenerationContext context)
        {
            var probabilityFunction = ProbabilityFunctionGenerator.Generate(context);
            
            var scaleOffset = new int[Scale.ScaleRankCount];
            for (var rank = 0; rank < scaleOffset.Length; rank++)
            {
                var rankProbability = probabilityFunction.GetProbability(rank);
                var testProbability = context.Random.NextDouble();
                if (ProbabilityTester.TestProbability(testProbability, rankProbability))
                {
                    scaleOffset[rank] = context.Random.Next(-2, 2);
                }
            }

            return scaleOffset;
        }
    }
}

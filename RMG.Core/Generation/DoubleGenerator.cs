namespace RMG.Core.Generation
{
    public sealed class DoubleGenerator : IGenerator
    {
        public double Min { get; set; }

        public double Max { get; set; }

        object IGenerator.Generate(GenerationContext context)
        {
            return Generate(context);
        }

        public double Generate(GenerationContext context)
        {
            return Min + context.Random.NextDouble() * (Max - Min);
        }
    }
}

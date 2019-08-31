namespace RMG.Core.Generation
{
    public sealed class DoubleMultiplierGenerator : IGenerator
    {
        public IGenerator LeftGenerator { get; set; }

        public IGenerator RightGenerator { get; set; }

        object IGenerator.Generate(GenerationContext context)
        {
            return Generate(context);
        }

        public double Generate(GenerationContext context)
        {
            var left = LeftGenerator.RunGeneration<double>(context);
            var right = RightGenerator.RunGeneration<double>(context);
            return left * right;
        }
    }
}

namespace RMG.Core.Generation
{
    public sealed class DoubleAddGenerator : IGenerator
    {
        public IGenerator ValueGenerator { get; set; }

        public IGenerator AdditiveGenerator { get; set; }

        object IGenerator.Generate(GenerationContext context)
        {
            return Generate(context);
        }

        public double Generate(GenerationContext context)
        {
            var value = ValueGenerator.RunGeneration<double>(context);
            var additive = AdditiveGenerator.RunGeneration<double>(context);
            return value + additive;
        }
    }
}

namespace RMG.Core.Generation.MathGenerators
{
    public sealed class DoubleAddGenerator : GeneratorBase<double>
    {
        public IGenerator<double> ValueGenerator { get; set; }

        public IGenerator<double> AdditiveGenerator { get; set; }

        public override double Generate(GenerationContext context)
        {
            var value = ValueGenerator.Generate(context);
            var additive = AdditiveGenerator.Generate(context);
            return value + additive;
        }
    }
}

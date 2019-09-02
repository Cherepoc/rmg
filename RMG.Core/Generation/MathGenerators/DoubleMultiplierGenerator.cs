namespace RMG.Core.Generation.MathGenerators
{
    public sealed class DoubleMultiplierGenerator : GeneratorBase<double>
    {
        public IGenerator<double> LeftGenerator { get; set; }

        public IGenerator<double> RightGenerator { get; set; }

        public override double Generate(GenerationContext context)
        {
            var left = LeftGenerator.Generate(context);
            var right = RightGenerator.Generate(context);
            return left * right;
        }
    }
}

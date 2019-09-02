namespace RMG.Core.Generation.MathGenerators
{
    public sealed class DoubleDivisionGenerator : GeneratorBase<double>
    {
        public IGenerator<double> DividentGenerator { get; set; }

        public IGenerator<double> DivisorGenerator { get; set; }

        public override double Generate(GenerationContext context)
        {
            var divident = DividentGenerator.Generate(context);
            var divisor = DivisorGenerator.Generate(context);
            return divident / divisor;
        }
    }
}

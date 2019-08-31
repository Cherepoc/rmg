namespace RMG.Core.Generation
{
    public sealed class DoubleDivisionGenerator : IGenerator
    {
        public IGenerator DividentGenerator { get; set; }

        public IGenerator DivisorGenerator { get; set; }

        object IGenerator.Generate(GenerationContext context)
        {
            return Generate(context);
        }

        public double Generate(GenerationContext context)
        {
            var divident = DividentGenerator.RunGeneration<double>(context);
            var divisor = DivisorGenerator.RunGeneration<double>(context);
            return divident / divisor;
        }
    }
}

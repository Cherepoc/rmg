namespace RMG.Core.Generation.RandomGenerators
{
    public sealed class DoubleGenerator : GeneratorBase<double>
    {
        public DoubleGenerator()
        {
        }

        public DoubleGenerator(double min, double max)
        {
            MinGenerator = new ConstantGenerator<double>(min);
            MaxGenerator = new ConstantGenerator<double>(max);
        }

        public IGenerator<double> MinGenerator { get; set; }

        public IGenerator<double> MaxGenerator { get; set; }

        public override double Generate(GenerationContext context)
        {
            var (min, max) = context.Generate(MinGenerator, MaxGenerator);
            return min + context.Random.NextDouble() * (max - min);
        }
    }
}

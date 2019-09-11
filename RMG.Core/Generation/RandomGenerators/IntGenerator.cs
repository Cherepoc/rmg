namespace RMG.Core.Generation.RandomGenerators
{
    public sealed class IntGenerator : GeneratorBase<int>
    {
        public IntGenerator()
        {
            
        }
        
        public IntGenerator(int min, int max)
        {
            MinGenerator = new ConstantGenerator<int>(min);
            MaxGenerator = new ConstantGenerator<int>(max);
        }
        
        public IGenerator<int> MinGenerator { get; set; }

        public IGenerator<int> MaxGenerator { get; set; }

        public override int Generate(GenerationContext context)
        {
            var (min, max) = context.Generate(MinGenerator, MaxGenerator);
            return context.Random.Next(min, max);
        }
    }
}

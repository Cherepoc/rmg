namespace RMG.Core.Generation.RandomGenerators
{
    public sealed class IntGenerator : GeneratorBase<int>
    {
        public IGenerator<int> MinGenerator { get; set; }

        public IGenerator<int> MaxGenerator { get; set; }

        public override int Generate(GenerationContext context)
        {
            var (min, max) = context.Generate(MinGenerator, MaxGenerator);
            return context.Random.Next(min, max);
        }
    }
}

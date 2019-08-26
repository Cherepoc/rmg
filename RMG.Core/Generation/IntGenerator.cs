namespace RMG.Core.Generation
{
    public sealed class IntGenerator : IGenerator
    {
        public int Min { get; set; }

        public int Max { get; set; }

        public IntGenerator()
        {
        }

        public IntGenerator(int min, int max)
        {
            Min = min;
            Max = max;
        }

        object IGenerator.Generate(GenerationContext context)
        {
            return Generate(context);
        }

        public int Generate(GenerationContext context)
        {
            return context.Random.Next(Min, Max);
        }
    }
}

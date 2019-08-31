namespace RMG.Core.Generation
{
    public sealed class ConditionalEqualGenerator : IGenerator
    {
        public IGenerator FirstGenerator { get; set; }
        public IGenerator SecondGenerator { get; set; }

        object IGenerator.Generate(GenerationContext context)
        {
            return Generate(context);
        }

        public bool Generate(GenerationContext context)
        {
            var first = FirstGenerator.RunGeneration(context);
            var second = SecondGenerator.RunGeneration(context);
            return Equals(first, second);
        }
    }
}

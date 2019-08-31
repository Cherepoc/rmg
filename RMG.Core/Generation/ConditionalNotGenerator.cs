namespace RMG.Core.Generation
{
    public sealed class ConditionalNotGenerator : IGenerator
    {
        public IGenerator ConditionGenerator { get; set; }

        object IGenerator.Generate(GenerationContext context)
        {
            return Generate(context);
        }

        public bool Generate(GenerationContext context)
        {
            var condition = ConditionGenerator.RunGeneration<bool>(context);
            return !condition;
        }
    }
}

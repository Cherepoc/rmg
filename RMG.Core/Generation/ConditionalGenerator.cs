namespace RMG.Core.Generation
{
    public sealed class ConditionalGenerator : IGenerator
    {
        public IGenerator ConditionGenerator { get; set; }

        public IGenerator TrueGenerator { get; set; }

        public IGenerator FalseGenerator { get; set; }

        public object Generate(GenerationContext context)
        {
            var condition = ConditionGenerator.RunGeneration<bool>(context);
            var generator = condition ? TrueGenerator : FalseGenerator;
            return generator.Generate(context);
        }
    }
}

namespace RMG.Core.Generation.LogicGenerators
{
    public sealed class BranchGenerator<T> : GeneratorBase<T>
    {
        public IGenerator<bool> ConditionGenerator { get; set; }

        public IGenerator<T> ThenGenerator { get; set; }

        public IGenerator<T> ElseGenerator { get; set; }

        public override T Generate(GenerationContext context)
        {
            var condition = ConditionGenerator.Generate(context);
            var generator = condition ? ThenGenerator : ElseGenerator;
            return generator.Generate(context);
        }
    }
}

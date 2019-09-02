namespace RMG.Core.Generation.LogicGenerators
{
    public sealed class BranchGenerator<T> : GeneratorBase<T>
    {
        public IGenerator<bool> ConditionGenerator { get; set; }

        public IGenerator<T> TrueGenerator { get; set; }

        public IGenerator<T> FalseGenerator { get; set; }

        public override T Generate(GenerationContext context)
        {
            var condition = ConditionGenerator.Generate(context);
            var generator = condition ? TrueGenerator : FalseGenerator;
            return generator.Generate(context);
        }
    }
}

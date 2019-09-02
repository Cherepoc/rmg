namespace RMG.Core.Generation.LogicGenerators
{
    public sealed class NotGenerator : GeneratorBase<bool>
    {
        public IGenerator<bool> ConditionGenerator { get; set; }

        public override bool Generate(GenerationContext context)
        {
            var condition = ConditionGenerator.Generate(context);
            return !condition;
        }
    }
}

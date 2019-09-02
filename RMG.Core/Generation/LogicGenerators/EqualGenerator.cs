namespace RMG.Core.Generation.LogicGenerators
{
    public sealed class EqualGenerator<T1, T2> : GeneratorBase<bool>
    {
        public IGenerator<T1> FirstGenerator { get; set; }
        public IGenerator<T2> SecondGenerator { get; set; }

        public override bool Generate(GenerationContext context)
        {
            var first = FirstGenerator.Generate(context);
            var second = SecondGenerator.Generate(context);
            return first.Equals(second);
        }
    }
}

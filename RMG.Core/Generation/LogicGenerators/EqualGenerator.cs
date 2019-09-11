namespace RMG.Core.Generation.LogicGenerators
{
    public sealed class EqualGenerator<T1, T2> : GeneratorBase<bool>
    {
        public IGenerator<T1> Value1Generator { get; set; }
        public IGenerator<T2> Value2Generator { get; set; }

        public override bool Generate(GenerationContext context)
        {
            var value1 = Value1Generator.Generate(context);
            var value2 = Value2Generator.Generate(context);
            return value1.Equals(value2);
        }
    }
}

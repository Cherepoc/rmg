namespace RMG.Core.Generation
{
    public abstract class BinaryOperatorGeneratorBase<T> : GeneratorBase<T>
    {
        public IGenerator<T> Value1Generator { get; set; }
        public IGenerator<T> Value2Generator { get; set; }

        protected abstract T Calculate(T value1, T value2);

        public override T Generate(GenerationContext context)
        {
            var (value1, value2) = context.Generate(Value1Generator, Value2Generator);
            return Calculate(value1, value2);
        }
    }
}

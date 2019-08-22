namespace RMG.Core.Generation
{
    public sealed class ConstantGenerator<T> : IGenerator
    {
        public ConstantGenerator()
        {
        }

        public ConstantGenerator(T value)
        {
            Value = value;
        }

        public T Value { get; set; }

        object IGenerator.Generate(GenerationContext context)
        {
            return Generate(context);
        }

        public T Generate(GenerationContext context)
        {
            return Value;
        }
    }
}

namespace RMG.Core.Generation
{
    public sealed class ConstantGenerator<T> : GeneratorBase<T>
    {
        public ConstantGenerator()
        {
        }

        public ConstantGenerator(T value)
        {
            Value = value;
        }

        public T Value { get; set; }

        public override T Generate(GenerationContext context)
        {
            return Value;
        }
    }
}

namespace RMG.Core.Generation.ConversionGenerators
{
    public abstract class ConverterGeneratorBase<TIn, TOut> : GeneratorBase<TOut>
    {
        public IGenerator<TIn> ValueGenerator { get; set; }

        public override TOut Generate(GenerationContext context)
        {
            var value = ValueGenerator.Generate(context);
            return Convert(value);
        }

        protected abstract TOut Convert(TIn value);
    }
}

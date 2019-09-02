namespace RMG.Core.Generation.ConversionGenerators
{
    public sealed class IntToDoubleConverterGenerator : ConverterGeneratorBase<int, double>
    {
        protected override double Convert(int value)
        {
            return value;
        }
    }
}

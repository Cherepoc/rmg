namespace RMG.Core.Generation.ConversionGenerators
{
    public sealed class DoubleToIntConverterGenerator : ConverterGeneratorBase<double, int>
    {
        protected override int Convert(double value)
        {
            return (int) value;
        }
    }
}

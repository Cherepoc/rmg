namespace RMG.Core.Generation.MathGenerators
{
    public sealed class DoubleMultiplicationGenerator : BinaryOperatorGeneratorBase<double>
    {
        protected override double Calculate(double value1, double value2)
        {
            return value1 * value2;
        }
    }
}

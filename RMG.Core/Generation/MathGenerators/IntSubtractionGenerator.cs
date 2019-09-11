namespace RMG.Core.Generation.MathGenerators
{
    public sealed class IntSubtractionGenerator : BinaryOperatorGeneratorBase<int>
    {
        protected override int Calculate(int value1, int value2)
        {
            return value1 - value2;
        }
    }
}

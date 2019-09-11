namespace RMG.Core.Generation.MathGenerators
{
    public sealed class IntAdditionGenerator : BinaryOperatorGeneratorBase<int>
    {
        protected override int Calculate(int value1, int value2)
        {
            return value1 + value2;
        }
    }
}

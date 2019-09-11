using System;

namespace RMG.Core.Generation.MathGenerators
{
    public sealed class DoublePowerGenerator : BinaryOperatorGeneratorBase<double>
    {
        protected override double Calculate(double value1, double value2)
        {
            return Math.Pow(value1, value2);
        }
    }
}

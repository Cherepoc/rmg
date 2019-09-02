using System;

namespace RMG.Core.Generation.MathGenerators
{
    public sealed class DoublePowerGenerator : GeneratorBase<double>
    {
        public IGenerator<double> ValueGenerator { get; set; }

        public IGenerator<double> PowerGenerator { get; set; }

        public override double Generate(GenerationContext context)
        {
            var value = ValueGenerator.Generate(context);
            var power = PowerGenerator.Generate(context);
            return Math.Pow(value, power);
        }
    }
}

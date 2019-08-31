using System;

namespace RMG.Core.Generation
{
    public sealed class DoublePowerGenerator : IGenerator
    {
        public IGenerator ValueGenerator { get; set; }

        public IGenerator PowerGenerator { get; set; }

        object IGenerator.Generate(GenerationContext context)
        {
            return Generate(context);
        }

        public double Generate(GenerationContext context)
        {
            var value = ValueGenerator.RunGeneration<double>(context);
            var power = PowerGenerator.RunGeneration<double>(context);
            return Math.Pow(value, power);
        }
    }
}

using System;

namespace RMG.Core.Generation.MathGenerators
{
    public sealed class DoubleMinGenerator : GeneratorBase<double>
    {
        public IGenerator<double> Value1Generator { get; set; }

        public IGenerator<double> Value2Generator { get; set; }

        public override double Generate(GenerationContext context)
        {
            var value1 = Value1Generator.Generate(context);
            var value2 = Value2Generator.Generate(context);
            return Math.Min(value1, value2);
        }
    }
}

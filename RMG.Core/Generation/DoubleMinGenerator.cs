using System;

namespace RMG.Core.Generation
{
    public sealed class DoubleMinGenerator : IGenerator
    {
        public IGenerator Value1Generator { get; set; }

        public IGenerator Value2Generator { get; set; }

        object IGenerator.Generate(GenerationContext context)
        {
            return Generate(context);
        }

        public double Generate(GenerationContext context)
        {
            var value1 = Value1Generator.RunGeneration<double>(context);
            var value2 = Value2Generator.RunGeneration<double>(context);
            return Math.Min(value1, value2);
        }
    }
}

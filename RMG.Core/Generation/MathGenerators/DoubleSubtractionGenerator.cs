namespace RMG.Core.Generation.MathGenerators
{
    public class DoubleSubtractionGenerator : GeneratorBase<double>
    {
        public IGenerator<double> SubtrahendGenerator { get; set; }

        public IGenerator<double> MinuendGenerator { get; set; }

        public override double Generate(GenerationContext context)
        {
            var subtrahend = SubtrahendGenerator.Generate(context);
            var minuend = MinuendGenerator.Generate(context);
            return subtrahend - minuend;
        }
    }
}

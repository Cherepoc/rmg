namespace RMG.Core.Generation
{
    public class DoubleSubtractionGenerator : IGenerator
    {
        public IGenerator SubtrahendGenerator { get; set; }

        public IGenerator MinuendGenerator { get; set; }

        object IGenerator.Generate(GenerationContext context)
        {
            return Generate(context);
        }

        public double Generate(GenerationContext context)
        {
            var subtrahend = SubtrahendGenerator.RunGeneration<double>(context);
            var minuend = MinuendGenerator.RunGeneration<double>(context);
            return subtrahend - minuend;
        }
    }
}

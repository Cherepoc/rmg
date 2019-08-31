using RMG.Core.Music;

namespace RMG.Core.Generation
{
    public sealed class DurationGenerator<T> : IGenerator
        where T : IDuration
    {
        object IGenerator.Generate(GenerationContext context)
        {
            return Generate(context);
        }

        public double Generate(GenerationContext context)
        {
            var durationEntity = context.FindParentValue<T>();
            return durationEntity.Duration;
        }
    }
}

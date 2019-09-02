using RMG.Core.Music;

namespace RMG.Core.Generation.ContextGenerators
{
    public sealed class ContextDurationGenerator<T> : GeneratorBase<double>
        where T : IDuration
    {
        public override double Generate(GenerationContext context)
        {
            var durationEntity = context.FindParentValue<T>();
            return durationEntity.Duration;
        }
    }
}

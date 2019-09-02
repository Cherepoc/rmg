using System;
using System.Collections.Generic;
using RMG.Core.Music;

namespace RMG.Core.Generation.RandomGenerators
{
    public sealed class SequentialTimelineGenerator<T> : GeneratorBase<IEnumerable<TimedEvent<T>>>
        where T : IDuration
    {
        public IGenerator<T> EventGenerator { get; set; }

        public override IEnumerable<TimedEvent<T>> Generate(GenerationContext context)
        {
            var parent = context.FindParentValue<IDuration>();
            if (parent == null)
            {
                throw new ApplicationException(
                    $"Cannot use {nameof(SequentialTimelineGenerator<T>)} outside of {nameof(IDuration)}");
            }

            var duration = parent.Duration;

            if (duration <= 0)
            {
                return Array.Empty<TimedEvent<T>>();
            }

            var result = new List<TimedEvent<T>>();
            var timelineContext = new GenerationContext(context, result);
            double totalDuration = 0;
            while (totalDuration < duration)
            {
                var newEvent = EventGenerator.Generate(timelineContext);
                result.Add(new TimedEvent<T>(totalDuration, newEvent));
                totalDuration += newEvent.Duration;
            }

            return result;
        }
    }
}

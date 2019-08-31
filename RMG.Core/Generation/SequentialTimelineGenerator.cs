using System;
using System.Collections.Generic;
using RMG.Core.Music;

namespace RMG.Core.Generation
{
    public sealed class SequentialTimelineGenerator<T> : IGenerator
        where T : IDuration
    {
        public IGenerator EventGenerator { get; set; }

        object IGenerator.Generate(GenerationContext context)
        {
            return Generate(context);
        }

        public IList<TimedEvent<T>> Generate(GenerationContext context)
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
                var newEvent = EventGenerator.RunGeneration<T>(timelineContext);
                // check if item should be added
                var newTotalDuration = totalDuration + newEvent.Duration;
                if (totalDuration == 0
                    || newTotalDuration <= duration
                    || duration - totalDuration > newTotalDuration - duration)
                {
                    result.Add(new TimedEvent<T>(totalDuration, newEvent));
                    totalDuration = newTotalDuration;
                }
                else
                {
                    break;
                }
            }

            return result;
        }
    }
}

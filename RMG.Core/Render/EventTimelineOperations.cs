using System;
using System.Collections.Generic;
using System.Linq;
using RMG.Core.Music;

namespace RMG.Core.Render
{
    public static class EventTimelineOperations
    {
        public static IReadOnlyList<TimedEvent<T>> SubTimeline<T>(
            IReadOnlyList<TimedEvent<T>> timeline,
            double position,
            double duration
        )
        {
            var orderedTimeline = timeline
                .OrderBy(x => x.Position)
                .ToList();
            var effectiveTimedEventIndex = GetLastElementIndexByPosition(orderedTimeline, position);
            var result = new List<TimedEvent<T>>();
            if (effectiveTimedEventIndex >= 0)
            {
                var effectiveTimedEvent = orderedTimeline[effectiveTimedEventIndex];
                result.Add(
                    new TimedEvent<T>
                    {
                        Event = effectiveTimedEvent.Event,
                        Position = 0
                    });
            }

            var endPosition = position + duration;
            result.AddRange(
                timeline
                    .Where(x => x.Position > position && x.Position < endPosition)
                    .Select(
                        x => new TimedEvent<T>
                        {
                            Event = x.Event,
                            Position = x.Position - position
                        }));

            return result;
        }

        public static IReadOnlyList<TimedEvent<T>> Merge<T>(
            IReadOnlyList<TimedEvent<T>> source,
            IReadOnlyList<TimedEvent<T>> target,
            double position,
            double duration,
            Func<T, T, T> mergeFunction,
            T defaultValue
        )
        {
            var orderedTarget = target
                .OrderBy(x => x.Position)
                .ToList();

            var newSource = source.Select(x => x.Copy()).ToList();

            if (target.Count == 0)
            {
                return newSource;
            }

            var sourceIndex = GetLastElementIndexByPosition(newSource, position);
            var sourceTimedEvent = sourceIndex >= 0 ? newSource[sourceIndex] : null;
            var targetIndex = 0;
            var targetTimedEvent = orderedTarget[targetIndex];
            var currentPosition = target[targetIndex].Position;
            while (currentPosition < duration)
            {
                var mergedTimedEvent = new TimedEvent<T>
                {
                    Position = currentPosition + position,
                    Event = sourceTimedEvent != null
                        ? mergeFunction(sourceTimedEvent.Event, targetTimedEvent.Event)
                        : targetTimedEvent.Event
                };
                if (sourceTimedEvent != null && sourceTimedEvent.Position == mergedTimedEvent.Position)
                {
                    newSource[sourceIndex] = mergedTimedEvent;
                }
                else
                {
                    newSource.Insert(++sourceIndex, mergedTimedEvent);
                }

                var nextSourceIndex = sourceIndex + 1;
                var nextSourceTimedItem = nextSourceIndex < newSource.Count ? newSource[nextSourceIndex] : null;
                var nextTargetIndex = targetIndex + 1;
                var nextTargetTimedItem = nextTargetIndex < orderedTarget.Count ? orderedTarget[nextTargetIndex] : null;

                var nextPosition = Math.Min(
                    nextTargetTimedItem?.Position ?? double.MaxValue,
                    (nextSourceTimedItem?.Position ?? double.MaxValue) - position);

                if (nextPosition >= duration)
                {
                    break;
                }

                if (nextTargetTimedItem?.Position == nextPosition)
                {
                    targetIndex = nextTargetIndex;
                    targetTimedEvent = nextTargetTimedItem;
                    currentPosition = nextTargetTimedItem.Position;
                }
                else if (nextSourceTimedItem?.Position == nextPosition + position)
                {
                    sourceIndex = nextSourceIndex;
                    sourceTimedEvent = nextSourceTimedItem;
                    currentPosition = sourceTimedEvent.Position - position;
                }
            }

            var endPosition = position + duration;
            var endEffectiveTimedEventIndex = GetLastElementIndexByPosition(newSource, endPosition);
            if (endEffectiveTimedEventIndex >= 0 && newSource.Count > 0)
            {
                var endEffectiveTimedEvent = newSource[endEffectiveTimedEventIndex];
                if (endEffectiveTimedEvent.Position < endPosition)
                {
                    newSource.Insert(
                        endEffectiveTimedEventIndex + 1,
                        new TimedEvent<T>
                        {
                            Position = endPosition,
                            Event = sourceTimedEvent != null ? sourceTimedEvent.Event : defaultValue
                        });
                }
            }

            return newSource;
        }

        private static int GetLastElementIndexByPosition<T>(IReadOnlyList<TimedEvent<T>> collection, double position)
        {
            for (var index = 0; index < collection.Count; index++)
            {
                var item = collection[index];
                if (item.Position > position)
                {
                    return index - 1;
                }
            }

            return collection.Count - 1;
        }
    }
}

using System.Collections.Generic;
using System.Linq;
using RMG.Core.Music;

namespace RMG.Core.Utils
{
    public static class TimedEventExtensions
    {
        public static T GetByPosition<T>(this IEnumerable<TimedEvent<T>> events, double position)
        {
            var enumeratedEvents = events as IReadOnlyCollection<TimedEvent<T>> ?? events.ToArray();
            var bound = enumeratedEvents.Where(x => x.Position <= position).Max(x => x.Position);
            // ReSharper disable once CompareOfFloatsByEqualityOperator
            return enumeratedEvents
                .Where(x => x.Position == bound)
                .Select(x => x.Event)
                .LastOrDefault();
        }
    }
}

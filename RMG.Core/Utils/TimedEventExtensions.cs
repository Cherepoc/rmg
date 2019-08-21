using System.Collections.Generic;
using System.Linq;
using RMG.Core.Music;

namespace RMG.Core.Utils
{
    public static class TimedEventExtensions
    {
        public static TimedEvent<T> GetEffectiveTimedEvent<T>(this IEnumerable<TimedEvent<T>> events, double position)
        {
            return events.LastOrDefault(x => x.Position <= position);
        }
        
        public static T GetEffectiveEvent<T>(this IEnumerable<TimedEvent<T>> events, double position)
        {
            return events.GetEffectiveEvent(position, default);
        }
        
        public static T GetEffectiveEvent<T>(this IEnumerable<TimedEvent<T>> events, double position, T defaultValue)
        {
            var lastTimedEvent = events.LastOrDefault(x => x.Position <= position);
            return lastTimedEvent != null ? lastTimedEvent.Event : defaultValue;
        }
    }
}

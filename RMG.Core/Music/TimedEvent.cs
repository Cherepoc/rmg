using System.Collections.Generic;

namespace RMG.Core.Music
{
    public sealed class TimedEvent<T> : ITimedEvent
    {
        public T Event { get; set; }
        public double Position { get; set; }

        object ITimedEvent.Event => Event;

        public TimedEvent<T> Copy()
        {
            return new TimedEvent<T>
            {
                Event = Event,
                Position = Position
            };
        }

        private bool Equals(TimedEvent<T> other)
        {
            return EqualityComparer<T>.Default.Equals(Event, other.Event)
                   && Position.Equals(other.Position);
        }

        public override bool Equals(object obj)
        {
            return ReferenceEquals(this, obj) || obj is TimedEvent<T> other && Equals(other);
        }

        public override int GetHashCode()
        {
            unchecked
            {
                return (EqualityComparer<T>.Default.GetHashCode(Event) * 397) ^ Position.GetHashCode();
            }
        }

        public static bool operator ==(TimedEvent<T> left, TimedEvent<T> right)
        {
            return Equals(left, right);
        }

        public static bool operator !=(TimedEvent<T> left, TimedEvent<T> right)
        {
            return !Equals(left, right);
        }

        public override string ToString()
        {
            return $"{{{Position}: {Event}}}";
        }
    }
}

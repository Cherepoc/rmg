namespace RMG.Core.Music
{
    public sealed class TimedEvent<T> : ITimedEvent
    {
        public T Event { get; set; }
        public double Position { get; set; }

        object ITimedEvent.Event => Event;
    }
}

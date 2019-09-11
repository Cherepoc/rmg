namespace RMG.Core.Music
{
    public interface ITimedEvent
    {
        double Position { get; set; }

        object Event { get; }
    }
    
    public interface ITimedEvent<out T> : ITimedEvent
    {
        new T Event { get; }
    }
}

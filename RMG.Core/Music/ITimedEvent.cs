namespace RMG.Core.Music
{
    public interface ITimedEvent
    {
        double Position { get; set; }

        object Event { get; }
    }
}

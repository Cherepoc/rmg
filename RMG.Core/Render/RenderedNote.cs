namespace RMG.Core.Render
{
    public sealed class RenderedNote
    {
        public RenderedNote(int offset, double volume, double duration)
        {
            Offset = offset;
            Volume = volume;
            Duration = duration;
        }

        public int Offset { get; }
        public double Volume { get; }
        public double Duration { get; }
    }
}

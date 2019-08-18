namespace RMG.Core.Music
{
    public sealed class Note : IDuration
    {
        public ScaleNoteOffset ScaleOffset { get; set; }

        public int Octave { get; set; }

        public double Volume { get; set; }

        public double Duration { get; set; }
    }
}

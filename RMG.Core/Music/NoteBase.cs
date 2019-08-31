namespace RMG.Core.Music
{
    public sealed class NoteBase
    {
        public int Key { get; set; }

        public int[] ScaleOffset { get; set; }

        public int Octave { get; set; }

        public double Volume { get; set; }
    }
}

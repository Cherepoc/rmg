namespace RMG.Core.Music
{
    public sealed class Note : IDuration
    {
        public int[] ScaleOffset { get; set; }

        public int Octave { get; set; }

        public double Volume { get; set; }

        public double Duration { get; set; }
    }
}

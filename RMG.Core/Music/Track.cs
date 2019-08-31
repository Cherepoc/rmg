namespace RMG.Core.Music
{
    public sealed class Track
    {
        public Instrument Instrument { get; set; }

        public NoteBase NoteBase { get; set; }

        public int MinOctave { get; set; }

        public int MaxOctave { get; set; }
    }
}

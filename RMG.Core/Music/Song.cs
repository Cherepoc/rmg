using System.Collections.Generic;

namespace RMG.Core.Music
{
    public sealed class Song : IDuration
    {
        public Scale Scale { get; set; }

        public IList<Track> Tracks { get; set; }

        public IList<TimedEvent<Part>> Parts { get; set; }

        public NoteBasePattern NoteBasePattern { get; set; }

        public double Tempo { get; set; }

        public int Key { get; set; }

        public int[] ScaleNoteOffset { get; set; }

        public int Octave { get; set; }

        public double Volume { get; set; }

        public double Duration { get; set; }
        
        public IList<Part> PartTemplates { get; set; }
        
        public IList<Pattern> PatternTemplates { get; set; }
    }
}

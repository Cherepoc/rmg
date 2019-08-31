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

        public NoteBase NoteBase { get; set; }

        public IList<IList<Part>> RankedPartTemplates { get; set; }

        public IList<IList<Pattern>> RankedPatternTemplates { get; set; }

        public double Duration { get; set; }
    }
}

using System.Collections.Generic;

namespace RMG.Core.Music
{
    public sealed class Song : IDuration
    {
        public Scale Scale { get; set; }

        public IReadOnlyList<Track> Tracks { get; set; }

        public IReadOnlyList<TimedEvent<Part>> Parts { get; set; }

        public NoteBasePattern NoteBasePattern { get; set; }

        public double Tempo { get; set; }

        public NoteBase NoteBase { get; set; }

        public IReadOnlyList<IReadOnlyList<Part>> RankedPartTemplates { get; set; }

        public IReadOnlyList<IReadOnlyList<Pattern>> RankedPatternTemplates { get; set; }

        public double Duration { get; set; }
    }
}

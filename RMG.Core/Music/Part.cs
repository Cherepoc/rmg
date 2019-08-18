using System.Collections.Generic;

namespace RMG.Core.Music
{
    public sealed class Part : IDuration
    {
        public IReadOnlyDictionary<Track, IReadOnlyList<TimedEvent<Pattern>>> TrackPatterns { get; set; }

        public IReadOnlyList<TimedEvent<Part>> Parts { get; set; }

        public NoteBasePattern NoteBasePattern { get; set; }
        public double Duration { get; set; }
    }
}

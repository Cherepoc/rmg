using System.Collections.Generic;

namespace RMG.Core.Music
{
    public sealed class Part : IDuration
    {
        public IDictionary<Track, IList<TimedEvent<Pattern>>> TrackPatterns { get; set; }

        public IList<TimedEvent<Part>> Parts { get; set; }

        public NoteBasePattern NoteBasePattern { get; set; }

        public double Duration { get; set; }
    }
}

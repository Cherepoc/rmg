using System.Collections.Generic;

namespace RMG.Core.Music
{
    public sealed class NoteBasePattern : IDuration
    {
        public IReadOnlyList<TimedEvent<int>> Key { get; set; }

        public IReadOnlyList<TimedEvent<int>> Octave { get; set; }

        public IReadOnlyList<TimedEvent<double>> Volume { get; set; }
        public double Duration { get; set; }
    }
}

using System.Collections.Generic;

namespace RMG.Core.Music
{
    public sealed class Pattern : IDuration
    {
        public IList<TimedEvent<Note>> Notes { get; set; }

        public IList<TimedEvent<Pattern>> Patterns { get; set; }

        public NoteBasePattern NoteBasePattern { get; set; }

        public double Duration { get; set; }
    }
}

using System.Collections.Generic;

namespace RMG.Core.Music
{
    public sealed class Pattern : IDuration
    {
        public IReadOnlyList<TimedEvent<Note>> Notes { get; set; }

        public IReadOnlyList<TimedEvent<Pattern>> Patterns { get; set; }

        public NoteBasePattern NoteBasePattern { get; set; }

        public double Duration { get; set; }
    }
}

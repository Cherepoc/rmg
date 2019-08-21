using System.Collections.Generic;
using RMG.Core.Music;

namespace RMG.Core.Render
{
    internal sealed class FlattenedTrackTimeline
    {
        public FlattenedTrackTimeline(
            Track track,
            IReadOnlyList<TimedEvent<Note>> noteTimeline,
            NoteBasePattern noteBasePattern
        )
        {
            Track = track;
            NoteTimeline = noteTimeline;
            NoteBasePattern = noteBasePattern;
        }

        public Track Track { get; }

        public IReadOnlyList<TimedEvent<Note>> NoteTimeline { get; set; }

        public NoteBasePattern NoteBasePattern { get; set; }
    }
}

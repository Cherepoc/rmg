using System.Collections.Generic;
using RMG.Core.Music;

namespace RMG.Core.Render
{
    public sealed class RenderedTrack
    {
        public RenderedTrack(int instrumentCode, IReadOnlyList<TimedEvent<RenderedNote>> noteTimeline)
        {
            InstrumentCode = instrumentCode;
            NoteTimeline = noteTimeline;
        }

        public int InstrumentCode { get; }

        public IReadOnlyList<TimedEvent<RenderedNote>> NoteTimeline { get; }
    }
}

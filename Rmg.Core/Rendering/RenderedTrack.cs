using Rmg.Core.Events;

namespace Rmg.Core.Rendering;

public sealed class RenderedTrack
{
    public RenderedTrack(bool isPercussionInstrument, int pitchInstrumentCode, EventTimeline<RenderedNote> noteTimeline)
    {
        IsPercussionInstrument = isPercussionInstrument;
        PitchInstrumentCode = pitchInstrumentCode;
        NoteTimeline = noteTimeline;
    }

    public bool IsPercussionInstrument { get; }
    public int PitchInstrumentCode { get; }
    public EventTimeline<RenderedNote> NoteTimeline { get; }
}

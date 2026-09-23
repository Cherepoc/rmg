using Rmg.Core.Events;

namespace Rmg.Core.Rendering;

public sealed class RenderedTrack
{
    public RenderedTrack(
        bool isPercussionInstrument,
        int pitchInstrumentCode,
        EventTimeline<RenderedNote> noteTimeline,
        double volume = 1
    )
    {
        ArgumentOutOfRangeException.ThrowIfNegative(volume);
        ArgumentOutOfRangeException.ThrowIfGreaterThan(volume, 1);

        IsPercussionInstrument = isPercussionInstrument;
        PitchInstrumentCode = pitchInstrumentCode;
        NoteTimeline = noteTimeline;
        Volume = volume;
    }

    public bool IsPercussionInstrument { get; }
    public int PitchInstrumentCode { get; }
    public EventTimeline<RenderedNote> NoteTimeline { get; }

    /// <summary>
    ///     How loud the track plays, as a part of the volume it plays at unasked. 1 asks for nothing and
    ///     leaves the dynamics of the notes to say everything, and anything less is written as a volume the
    ///     whole track plays under.
    /// </summary>
    public double Volume { get; }
}

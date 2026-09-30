using System.Collections.Immutable;
using Rmg.Core.Events;
using Rmg.Core.Songs;

namespace Rmg.Core.Rendering;

public sealed class RenderedTrack
{
    public RenderedTrack(
        bool isPercussionInstrument,
        TrackRole role,
        int pitchInstrumentCode,
        EventTimeline<RenderedNote> noteTimeline,
        double pan,
        double volume = 1
    )
    {
        ArgumentOutOfRangeException.ThrowIfLessThan(pan, -1);
        ArgumentOutOfRangeException.ThrowIfGreaterThan(pan, 1);
        ArgumentOutOfRangeException.ThrowIfNegative(volume);
        ArgumentOutOfRangeException.ThrowIfGreaterThan(volume, 1);

        IsPercussionInstrument = isPercussionInstrument;
        Role = role;
        PitchInstrumentCode = pitchInstrumentCode;
        NoteTimeline = noteTimeline;
        Pan = pan;
        Volume = volume;
    }

    public bool IsPercussionInstrument { get; }

    /// <summary>The part the track plays, the drums' for the percussion track.</summary>
    public TrackRole Role { get; }
    public int PitchInstrumentCode { get; }
    public EventTimeline<RenderedNote> NoteTimeline { get; }

    /// <summary>The track's changes of instrument after its first, each at its place: none for a track of one instrument throughout.</summary>
    public ImmutableArray<(double Position, int Program)> ProgramChanges { get; init; } = [];

    /// <summary>Where the track sits from left to right, from -1, left, through 0, the middle, to 1, right.</summary>
    public double Pan { get; }

    /// <summary>
    ///     How loud the track plays, as a part of the volume it plays at unasked. 1 asks for nothing and
    ///     leaves the dynamics of the notes to say everything, and anything less is written as a volume the
    ///     whole track plays under.
    /// </summary>
    public double Volume { get; }
}

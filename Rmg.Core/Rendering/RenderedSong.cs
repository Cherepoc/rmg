using System.Collections.Immutable;
using Rmg.Core.Events;

namespace Rmg.Core.Rendering;

public sealed class RenderedSong
{
    public RenderedSong(
        double duration,
        StateTimeline<double> tempoTimeline,
        ImmutableArray<RenderedTrack> tracks
    )
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(duration);
        if (!tempoTimeline.IsEmpty && tempoTimeline.StateKind != StateKinds.Tempo)
            throw new ArgumentException("Tempo timeline must be of kind Tempo.", nameof(tempoTimeline));

        Duration = duration;
        TempoTimeline = tempoTimeline;
        Tracks = tracks;
    }

    public double Duration { get; }

    public StateTimeline<double> TempoTimeline { get; }

    public ImmutableArray<RenderedTrack> Tracks { get; }
}

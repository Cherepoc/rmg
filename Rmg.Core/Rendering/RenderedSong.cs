using System.Collections.Immutable;
using Rmg.Core.Events;

namespace Rmg.Core.Rendering;

public sealed class RenderedSong
{
    public RenderedSong(
        double duration,
        StateTimeline<double> tempoTimeline,
        StateTimeline<double> fadeTimeline,
        ImmutableArray<RenderedTrack> tracks
    )
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(duration);
        if (!tempoTimeline.IsDefault && tempoTimeline.StateKind != StateKinds.Tempo)
            throw new ArgumentException("Tempo timeline must be of kind Tempo.", nameof(tempoTimeline));
        if (!fadeTimeline.IsDefault && fadeTimeline.StateKind != StateKinds.Fade)
            throw new ArgumentException("Fade timeline must be of kind Fade.", nameof(fadeTimeline));

        Duration = duration;
        TempoTimeline = tempoTimeline;
        FadeTimeline = fadeTimeline;
        Tracks = tracks;
    }

    public double Duration { get; }

    public StateTimeline<double> TempoTimeline { get; }

    /// <summary>How far the whole band is faded in, as the song fades out; no steps for a song that does not.</summary>
    public StateTimeline<double> FadeTimeline { get; }

    public ImmutableArray<RenderedTrack> Tracks { get; }
}

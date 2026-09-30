using System.Collections.Immutable;
using Rmg.Core.Composition;
using Rmg.Core.Events;

namespace Rmg.Core.Rendering;

public sealed class RenderedSong
{
    public RenderedSong(
        double duration,
        ImmutableArray<(double Position, Meter Meter)> meters,
        StateTimeline<double> tempoTimeline,
        StateTimeline<double> fadeTimeline,
        ImmutableArray<RenderedTrack> tracks
    )
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(duration);
        if (meters.IsDefaultOrEmpty || meters[0].Position != 0)
            throw new ArgumentException("A song's meters start with the one it starts in.", nameof(meters));
        if (!tempoTimeline.IsDefault && tempoTimeline.StateKind != StateKinds.Tempo)
            throw new ArgumentException("Tempo timeline must be of kind Tempo.", nameof(tempoTimeline));
        if (!fadeTimeline.IsDefault && fadeTimeline.StateKind != StateKinds.Fade)
            throw new ArgumentException("Fade timeline must be of kind Fade.", nameof(fadeTimeline));

        Duration = duration;
        Meters = meters;
        TempoTimeline = tempoTimeline;
        FadeTimeline = fadeTimeline;
        Tracks = tracks;
    }

    public double Duration { get; }

    /// <summary>The meter the song starts in.</summary>
    public Meter Meter => Meters[0].Meter;

    /// <summary>The meter the song's bars are in from where it starts and every change, which its MIDI file writes as its time signatures.</summary>
    public ImmutableArray<(double Position, Meter Meter)> Meters { get; }

    public StateTimeline<double> TempoTimeline { get; }

    /// <summary>How far the whole band is faded in, as the song fades out; no steps for a song that does not.</summary>
    public StateTimeline<double> FadeTimeline { get; }

    public ImmutableArray<RenderedTrack> Tracks { get; }
}

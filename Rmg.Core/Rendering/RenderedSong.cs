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

    /// <summary>The room its reverb is, as the Roland GS reverb macro that sets it, none for the player's own.</summary>
    public int? Room { get; init; }

    /// <summary>The effect one part plays through, and the part, written as Roland GS messages; none for none.</summary>
    public Insertion? Insertion { get; init; }

    /// <summary>How long the echo waits, in beats, which the Roland GS delay plays at the song's tempo; none for a song that does not echo.</summary>
    public double? Echo { get; init; }

    public double Duration { get; }

    /// <summary>The meter the song starts in.</summary>
    public Meter Meter => Meters[0].Meter;

    /// <summary>The meter the song's bars are in from where it starts and every change, which its MIDI file writes as its time signatures.</summary>
    public ImmutableArray<(double Position, Meter Meter)> Meters { get; }

    public StateTimeline<double> TempoTimeline { get; }

    /// <summary>How long the song plays, in seconds: every stretch of its beats at the tempo it plays at there.</summary>
    public double Seconds => SecondsAt(Duration);

    /// <summary>How far into the song a place is, in seconds: every stretch of its beats before it at the tempo it plays at there.</summary>
    public double SecondsAt(double position)
    {
        var changes = TempoTimeline.Select(x => x.Position).Where(x => x > 0 && x < position).Prepend(0).Append(position).Distinct().Order().ToArray();
        return changes.Zip(changes.Skip(1)).Sum(x => (x.Second - x.First) * Meter.SecondsPerBeat(TempoTimeline.GetEffectiveValueAt(x.First)));
    }

    /// <summary>How far the whole band is faded in, as the song fades out; no steps for a song that does not.</summary>
    public StateTimeline<double> FadeTimeline { get; }

    public ImmutableArray<RenderedTrack> Tracks { get; }
}

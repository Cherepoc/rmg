using System.Collections.Immutable;
using Rmg.Core.Events;
using Rmg.Core.Probabilities;
using Rmg.Core.Songs;

namespace Rmg.Core.Composition;

/// <summary>
///     A line's notes stacked now and then, the band's chord under them, where its instrument plays chords
///     (<see cref="CompositionStateKinds.LineVoices" />): a riff landing on a chord where a section starts, as a guitar hits
///     a power chord with the crash, and a melody leaning on a double stop on every bar's downbeat of a section, as a
///     pianist or a guitarist plays a tune. Neither plainer nor wilder, it does not lean by the song's unconventionality.
/// </summary>
internal static class LineChords
{
    /// <summary>How a line stacks: the chance a section does, whether on every bar's downbeat or its first note alone, and how many notes, its own and the chord's under it.</summary>
    public sealed record Way(double Chance, bool EveryBar, ImmutableArray<Weighted<int>> Voices);

    /// <summary>The riff, landing on a chord half the time where a section starts, two or three notes.</summary>
    public static Way Riff { get; } = new(0.5, false, [new(0.6, 2), new(0.4, 3)]);

    /// <summary>The melody, in a quarter of its sections, a double stop on every bar's downbeat.</summary>
    public static Way Melody { get; } = new(0.25, true, [new(1, 2)]);

    /// <summary>The song with a line's notes stacked as it stacks them, each section drawing from a sequence of its own.</summary>
    /// <param name="streams">A section's sequence, by its place among the song's sections.</param>
    public static TrackEventStateTimelineMap<StateMap> Apply(
        TrackEventStateTimelineMap<StateMap> song,
        int lineTrack,
        PitchInstrumentTrack line,
        SongMap map,
        Func<int, IGenerationContext> streams,
        Way way
    )
    {
        if (!InstrumentRoles.PlaysChords(line.InstrumentCode) || !song.TrackTimelineMap.ContainsKey(lineTrack))
            return song;

        // the places a line stacks on, a section's start or every bar's downbeat, and how many notes it stacks there
        var landings = new Dictionary<double, int>();
        // the sections and an intro that plays as one, as its source, not the ending's final chord
        foreach (var (index, span, _) in map.Plays.Where(x => !x.IsEnding))
        {
            var context = streams(index);
            var stacks = context.TestProbability(way.Chance);
            var voices = context.Pick(way.Voices);
            if (!stacks)
                continue;
            var bars = way.EveryBar ? span.BarCount : 1;
            for (var bar = 0; bar < bars; bar++)
                landings[Math.Round(span.Start + bar * span.Meter.BarDuration, 6)] = voices;
        }

        return song.MapTrackEvents(new Dictionary<int, Func<EventTimeline<StateMap>, EventTimeline<StateMap>>>
        {
            [lineTrack] = notes => EventTimeline.Create(notes.Duration, notes.Select(x => landings.TryGetValue(Math.Round(x.Position, 6), out var voices)
                ? x.Value.With(CompositionStateKinds.LineVoices, voices).ToTimelineItem(x.Position)
                : x))
        });
    }
}

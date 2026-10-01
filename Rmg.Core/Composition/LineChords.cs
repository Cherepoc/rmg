using System.Collections.Immutable;
using Rmg.Core.Events;
using Rmg.Core.Probabilities;
using Rmg.Core.Songs;

namespace Rmg.Core.Composition;

/// <summary>
///     A riff landing on a chord where a section starts, now and then, as a guitar hits a power chord with the crash: its
///     note there stacked, the band's chord under it, where its instrument plays chords (<see cref="CompositionStateKinds.LineVoices" />).
///     Neither plainer nor wilder, it does not lean by the song's unconventionality.
/// </summary>
internal static class LineChords
{
    /// <summary>The chance a riff stacks its note where a section starts.</summary>
    public const double LandingChance = 0.5;

    /// <summary>How many notes it stacks, its own and the chord's under it.</summary>
    public static ImmutableArray<Weighted<int>> Voices { get; } = [new(0.6, 2), new(0.4, 3)];

    /// <summary>The song with the riff's notes stacked where its sections start, each section drawing from a sequence of its own.</summary>
    /// <param name="streams">A section's sequence, by its place among the song's sections.</param>
    public static TrackEventStateTimelineMap<StateMap> Apply(
        TrackEventStateTimelineMap<StateMap> song,
        int riffTrack,
        PitchInstrumentTrack riff,
        SongMap map,
        Func<int, IGenerationContext> streams
    )
    {
        if (!InstrumentRoles.PlaysChords(riff.InstrumentCode) || !song.TrackTimelineMap.ContainsKey(riffTrack))
            return song;

        // the sections' starts the riff lands on, and how many notes it stacks there
        var landings = new Dictionary<double, int>();
        foreach (var (span, index) in map.Sections.Select((x, i) => (x, i)))
        {
            var context = streams(index);
            var lands = context.TestProbability(LandingChance);
            var voices = context.Pick(Voices);
            if (lands)
                landings[Math.Round(span.Start, 6)] = voices;
        }

        return song.MapTrackEvents(new Dictionary<int, Func<EventTimeline<StateMap>, EventTimeline<StateMap>>>
        {
            [riffTrack] = notes => EventTimeline.Create(notes.Duration, notes.Select(x => landings.TryGetValue(Math.Round(x.Position, 6), out var voices)
                ? x.Value.With(CompositionStateKinds.LineVoices, voices).ToTimelineItem(x.Position)
                : x))
        });
    }
}

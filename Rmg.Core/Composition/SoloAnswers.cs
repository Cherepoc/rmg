using Rmg.Core.Events;
using Rmg.Core.Probabilities;
using Rmg.Core.Songs;

namespace Rmg.Core.Composition;

/// <summary>
///     The solo part answering the melody outside its solos, as a guitarist fills between a singer's phrases: in a section
///     the counter-melody leaves to it, now and then, by the melody facet, never in a plain song, it plays in the melody's
///     longer gaps, a rest or a note held, now and then, a short line of its own in its soloist's vocabulary
///     (<see cref="SoloLines" />), a held note of the chord in a short gap, a lick and a held note in a longer, under the
///     melody, ending before the melody goes on.
/// </summary>
internal static class SoloAnswers
{
    /// <summary>The chance a section answers the melody, where the counter-melody rests, by the melody facet.</summary>
    public static ByConvention Chance { get; } = new(0, 0.3, 0.3);

    /// <summary>The chance a gap of an answering section is answered, so that the answers stay sparse.</summary>
    public const double GapChance = 0.5;

    /// <summary>How long a gap between the melody's notes is, in beats, before it is answered.</summary>
    public const double ShortestGap = 2;

    /// <summary>How far into a gap an answer starts, in beats, on the half beat after, and how long before the melody goes on it ends.</summary>
    public const double After = 1;

    public const double Before = 0.25;

    /// <summary>How much quieter an answer plays than the melody of its section, on the average of their velocities as rendered.</summary>
    public const double Quieter = 0.06;

    /// <summary>The song with the solo part's answers played, each section drawing from a sequence of its own.</summary>
    /// <param name="answerer">The solo part's vocabulary, its soloist's.</param>
    /// <param name="melodyFacet">Every section's melody facet, by its place among the song's sections.</param>
    /// <param name="streams">A section's sequence, by its place.</param>
    public static TrackEventStateTimelineMap<StateMap> Apply(
        TrackEventStateTimelineMap<StateMap> song,
        IReadOnlyDictionary<int, TrackRole> roles,
        IReadOnlyDictionary<int, IInstrumentTrack> definitions,
        SongMap map,
        IReadOnlyDictionary<int, SoloPlan> solos,
        Soloist answerer,
        Func<int, double> melodyFacet,
        Func<int, IGenerationContext> streams
    )
    {
        var trackOf = roles.GroupBy(x => x.Value).ToDictionary(x => x.Key, x => x.First().Key);
        if (!trackOf.TryGetValue(TrackRole.Solo, out var soloTrack) || !song.TrackTimelineMap.TryGetValue(trackOf[TrackRole.Melody], out var melodyTrack))
            return song;

        // the solo part, which has no notes of its own, plays over the melody's state, as it does in a solo
        var track = song.TrackTimelineMap.GetValueOrDefault(soloTrack)
                    ?? EventStateTimelineMap.Create(song.Duration, EventTimeline.Create<StateMap>(song.Duration), melodyTrack.StateTimelineMap);
        var answers = new List<TimelineItem<StateMap>>();
        var melodyHeard = new Lazy<TimelineItem<StateMap>[]>(() => [..Realizer.GetNoteStates(melodyTrack, definitions[trackOf[TrackRole.Melody]], song.CommonStateTimelineMap)]);
        foreach (var (span, index) in map.Sections.Select((x, i) => (x, i)))
        {
            var context = streams(index);
            var answering = context.TestProbability(Chance.At(melodyFacet(index)));
            bool IsIn(TimelineItem<StateMap> note) => note.Position >= span.Start - 1e-9 && note.Position < span.End - 1e-9;
            var counterRests = !trackOf.TryGetValue(TrackRole.CounterMelody, out var counter) || !song.TrackTimelineMap.TryGetValue(counter, out var counterTrack) || !counterTrack.EventTimeline.Any(IsIn);
            var melody = melodyTrack.EventTimeline.Where(IsIn).ToArray();
            var band = song.TrackTimelineMap.TryGetValue(trackOf[TrackRole.Chords], out var chords) ? chords.EventTimeline.Where(IsIn).ToArray() : [];
            if (!answering || solos.ContainsKey(index) || !counterRests || melody.Length == 0)
                continue;

            // the gaps between the melody's notes, and the last to the section's end, each answered from a beat in, under the
            // melody as it is heard there
            var heard = Solos.Loudness(melodyHeard.Value.Where(IsIn), 0);
            var onsets = melody.Select(x => x.Position).Append(span.End).ToArray();
            for (var i = 0; i + 1 < onsets.Length; i++)
            {
                var answered = context.TestProbability(GapChance);
                var start = Math.Ceiling(2 * (onsets[i] + After) - 1e-9) / 2;
                var end = onsets[i + 1] - Before;
                if (!answered || onsets[i + 1] - onsets[i] < ShortestGap - 1e-9 || end - start < 1 - 1e-9)
                    continue;

                var gap = span with { Start = start, Duration = end - start };
                var (line, _) = SoloLines.Generate(context, context, context, gap, answerer, true, melody, band, track, (PitchInstrumentTrack)definitions[soloTrack], song.CommonStateTimelineMap);
                var lift = Solos.Lift(
                    Realizer.GetNoteStates(EventStateTimelineMap.Create(song.Duration, EventTimeline.Create(song.Duration, line), track.StateTimelineMap), definitions[soloTrack], song.CommonStateTimelineMap).ToArray(),
                    heard - Quieter);
                // a line of the part's own, not a solo's: neither in the melody's octaves nor ending on a solo's release
                answers.AddRange(line.Select(x => x.Value
                    .With(StateKinds.Velocity, x.Value.GetStateValue(StateKinds.Velocity) + lift)
                    .With(CompositionStateKinds.Octave, 0)
                    .With(CompositionStateKinds.SoloRelease, 0)
                    .ToTimelineItem(x.Position)));
            }
        }

        if (answers.Count == 0)
            return song;

        var played = EventTimeline.Create(song.Duration, track.EventTimeline.Concat(answers).OrderBy(x => x.Position));
        return TrackEventStateTimelineMap.Create(song.Duration, song.TrackTimelineMap.SetItem(soloTrack, track.WithEvents(played)), song.CommonStateTimelineMap);
    }
}

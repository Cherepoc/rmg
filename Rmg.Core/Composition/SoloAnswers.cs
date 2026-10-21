using Rmg.Core.Events;
using Rmg.Core.Probabilities;
using Rmg.Core.Songs;

namespace Rmg.Core.Composition;

/// <summary>
///     The solo part answering the melody outside its solos, as a guitarist fills between a singer's phrases: in a section
///     the counter-melody leaves to it, now and then, by the melody facet, never in a plain song, it plays in the melody's
///     gaps of more than two beats between its phrases, a rest or a phrase's last note held, now and then, a short line
///     of its own in its soloist's vocabulary (<see cref="SoloLines" />), planned over the gap as a solo's phrase is,
///     under the melody, starting on the half beat a beat into the gap and ending before the melody goes on.
/// </summary>
internal static class SoloAnswers
{
    /// <summary>The chance a section answers the melody, where the counter-melody rests, by the melody facet.</summary>
    public static ByConvention Chance { get; } = new(0, 0.3, 0.3);

    /// <summary>What an answer keeps out of: another part's answer to a calling verse.</summary>
    private const Hold KeepsOutOf = Hold.Answer;

    /// <summary>The place an answer's draws of its pickups are derived from its line's by, a sequence of its own.</summary>
    private const long PickupStream = 1;

    /// <summary>The chance a gap of an answering section is answered, so that the answers stay sparse.</summary>
    public const double GapChance = 0.5;

    /// <summary>
    ///     How far into a gap an answer starts, in beats, on the half beat after, and how long before the melody goes on it
    ///     ends: a gap answered is at least a beat longer than both.
    /// </summary>
    public const double After = 1;

    public const double Before = 0.25;

    /// <summary>How far before a phrase's start the melody's next note may come and the gap still be the one between its phrases, its pickup, in beats.</summary>
    public const double Pickup = 1;

    /// <summary>How much quieter an answer plays than the melody of its section, on the average of their velocities as rendered.</summary>
    public const double Quieter = 0.06;

    /// <summary>How many bars an answer plays at most, a short line in a gap, never a solo.</summary>
    public const int LongestBars = 2;

    /// <summary>The song with the solo part's answers played, each section drawing from a sequence of its own.</summary>
    /// <param name="answerer">The solo part's vocabulary, its soloist's.</param>
    /// <param name="melodyFacet">Every section's melody facet, by its place among the song's sections.</param>
    /// <param name="streams">A section's sequence, by its place, and its answers' voicings' and motifs', each of its own.</param>
    /// <param name="genre">The genre whose chance of answering a section draws (<see cref="GenreChoices.SoloAnswers" />).</param>
    /// <param name="holds">What the song holds, a calling verse's answer among it, which the answers keep out of.</param>
    public static TrackEventStateTimelineMap<StateMap> Apply(
        TrackEventStateTimelineMap<StateMap> song,
        IReadOnlyDictionary<int, TrackRole> roles,
        IReadOnlyDictionary<int, IInstrumentTrack> definitions,
        SongMap map,
        IReadOnlyDictionary<int, SoloPlan> solos,
        Soloist answerer,
        Func<int, double> melodyFacet,
        Func<int, (IGenerationContext Answers, IGenerationContext Voicing, IGenerationContext Motifs)> streams,
        Genre genre,
        Holds holds
    )
    {
        var trackOf = roles.TrackOf();
        if (!trackOf.TryGetValue(TrackRole.Solo, out var soloTrack) || !song.TrackTimelineMap.TryGetValue(trackOf[TrackRole.Melody], out var melodyTrack))
            return song;

        // the solo part, which has no notes of its own, plays over the melody's state, as it does in a solo
        var track = song.TrackTimelineMap.GetValueOrDefault(soloTrack)
                    ?? EventStateTimelineMap.Create(song.Duration, EventTimeline.Create<StateMap>(song.Duration), melodyTrack.StateTimelineMap);
        var answers = new List<TimelineItem<StateMap>>();
        var melodyHeard = new Lazy<TimelineItem<StateMap>[]>(() => [..Realizer.GetNoteStates(melodyTrack, definitions[trackOf[TrackRole.Melody]], song.CommonStateTimelineMap)]);
        foreach (var (span, index) in map.Sections.Select((x, i) => (x, i)))
        {
            var (context, voicing, motifs) = streams(index);
            var answering = context.TestProbability(Chance.At(melodyFacet(index), genre.Chance(GenreChoices.SoloAnswers, "sections")));
            bool IsIn(TimelineItem<StateMap> note) => note.IsWithin(span.Start, span.End);
            var counterRests = !trackOf.TryGetValue(TrackRole.CounterMelody, out var counter) || !song.TrackTimelineMap.TryGetValue(counter, out var counterTrack) || !counterTrack.EventTimeline.Any(IsIn);
            var melody = melodyTrack.EventTimeline.Where(IsIn).ToArray();
            // the chords, which an answer's stacked notes stack on
            var band = song.TrackTimelineMap.TryGetValue(trackOf[TrackRole.Chords], out var chords) ? chords.EventTimeline.Where(IsIn).ToArray() : [];
            // an answer over the band, any part of it but the lines, none where the melody plays alone
            var accompanied = song.TrackTimelineMap.Any(x => roles[x.Key] is not (TrackRole.Melody or TrackRole.CounterMelody or TrackRole.Solo or TrackRole.Drum)
                                                             && x.Value.EventTimeline.Any(IsIn));
            if (solos.ContainsKey(index) || !counterRests || melody.Length == 0 || !accompanied)
                continue;
            StateTrace.RecordSection(TracePoints.SoloAnswers, soloTrack, span.SectionId, $"{answering}", (index, answering));
            if (!answering)
                continue;

            // the gaps between the melody's notes, and the last to the section's end, each answered from a beat in, under the
            // melody as it is heard there
            var heard = Solos.Loudness(melodyHeard.Value.Where(IsIn), 0);
            var onsets = melody.Select(x => x.Position).Append(span.End).ToArray();
            // the phrases' starts after the section's, and its end, which a gap answered spans, the melody's next note at most
            // its pickup before it
            var phrases = span.Phrases.Select(x => x.Start).Skip(1).Append(span.End).ToArray();
            bool BetweenPhrases(int gap) => phrases.Any(x => x > onsets[gap] + 1e-9 && x <= onsets[gap + 1] + Pickup + 1e-9);
            // every gap's test drawn before any line, so that a line's draws leave which gaps are answered
            var gaps = Enumerable.Range(0, onsets.Length - 1).Select(_ => context.TestProbability(GapChance)).ToArray();
            // and every gap's line from sequences of its own, by its place, so that a gap passed over leaves the others' lines
            var (lineSeed, voicingSeed, motifSeed) = (context.GenerateSeed(), voicing.GenerateSeed(), motifs.GenerateSeed());
            for (var i = 0; i + 1 < onsets.Length; i++)
            {
                var answered = gaps[i];
                // on a half beat of the section's bars, counted from its start, as a short bar before it moves the song's grid
                var start = span.Start + Math.Ceiling(2 * (onsets[i] + After - span.Start) - 1e-9) / 2;
                // a short line, a bar or two at most, however long the gap
                var end = Math.Min(onsets[i + 1] - Before, start + LongestBars * span.Meter.BarDuration);
                // nor over another part's answer to a call
                if (!answered || !BetweenPhrases(i) || end - start < 1 - 1e-9 || holds.Meets(KeepsOutOf, start, end))
                    continue;

                var gap = span with { Start = start, Duration = end - start };
                // its strong beats on its section's bars
                var (line, _) = SoloLines.Generate(new GenerationContext(Seeds.Derive(lineSeed, i)), new GenerationContext(Seeds.Derive(voicingSeed, i)), new GenerationContext(Seeds.Derive(motifSeed, i)),
                    new GenerationContext(Seeds.Derive(Seeds.Derive(lineSeed, i), PickupStream)), gap, gap.Duration, span.Start, answerer, true, false, melody, band, track, (PitchInstrumentTrack)definitions[soloTrack], song.CommonStateTimelineMap);
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

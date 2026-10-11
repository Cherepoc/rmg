using Rmg.Core.Events;
using Rmg.Core.Probabilities;
using Rmg.Core.Songs;

namespace Rmg.Core.Composition;

/// <summary>
///     Call and response, as a blues, soul, funk, country or Latin band answers its singer: in a verse whose phrases split in
///     two, the melody sings every phrase's first half, its last note there held to the half rather than cut, and rests in
///     the second but for its walk into the next section, where the riff answers it, playing nothing in the first; where the song has no riff, the solo part's
///     answers fill the melody's gaps as they do (<see cref="SoloAnswers" />). A section's decision, the same wherever it
///     plays, by a genre's chance, the songs' own seldom and never in the plainest. A stage of the song after its lines are
///     placed (<see cref="SongStages" />).
/// </summary>
internal static class CallAndResponse
{
    /// <summary>The chance a verse calls and is answered, by the melody facet: never in the plainest songs, seldom in the others.</summary>
    public static ByConvention Chance { get; } = new(0, 0.05, 0.05);

    internal static SongDraft Apply(SongDecisions song, SongDraft draft)
    {
        var calling = song.SectionIds.Distinct().ToDictionary(id => id, id =>
        {
            var calls = song.Stream(SongStream.CallAndResponse, id).TestProbability(Chance.At(song.SectionOf(id).Facets[Facet.Melody], song.Genre.Chance(GenreChoices.CallAndResponse, "sections")))
                        && song.Structure.Roles[id] == SectionRole.Verse && song.SectionMeters[id].PhraseBars % 2 == 0;
            StateTrace.RecordSection(TracePoints.CallAndResponse, SectionGenerator.SectionTrace, id, $"{calls}", calls);
            return calls;
        });
        // every calling appearance's phrases, each split at its half, but in a solo
        var halves = song.Form.Map.Sections.Where((x, i) => calling[x.SectionId] && !song.Solos.ContainsKey(i))
            .SelectMany(span => Enumerable.Range(0, (int)Math.Ceiling(span.Duration / span.Meter.PhraseDuration - 1e-9))
                .Select(p => span.Start + p * span.Meter.PhraseDuration)
                .Select(start => (Start: start, Half: start + span.Meter.PhraseBars / 2 * span.Meter.BarDuration, End: Math.Min(start + span.Meter.PhraseDuration, span.End))))
            .ToArray();
        if (halves.Length == 0)
            return draft;

        var melody = song.Track(TrackRole.Melody).Key;
        var riff = song.Playing.Values.Contains(TrackRole.Riff) ? song.Track(TrackRole.Riff).Key : (int?)null;
        var edits = new Dictionary<int, Func<EventTimeline<StateMap>, EventTimeline<StateMap>>>
        {
            // the melody's first half, its last note held to the half, and its walk into the next section
            [melody] = notes => EventTimeline.Create(notes.Duration, notes
                .Where(x => x.Value.GetStateValue(CompositionStateKinds.Walk) != 0 || !halves.Any(h => x.IsWithin(h.Half, h.End)))
                .Select(x => halves.FirstOrDefault(h => x.IsWithin(h.Start, h.Half)) is { End: > 0 } phrase && !notes.Any(n => n.Position > x.Position + 1e-9 && n.IsWithin(phrase.Start, phrase.Half))
                    ? x.Value.With(StateKinds.HeldDuration, phrase.Half - x.Position).ToTimelineItem(x.Position)
                    : x))
        };
        // the riff's answer, its second half
        if (riff is { } answering)
            edits[answering] = notes => EventTimeline.Create(notes.Duration, notes.Where(x => !halves.Any(h => x.IsWithin(h.Start, h.Half))));
        return draft with { Timeline = draft.Timeline.MapTrackEvents(edits) };
    }
}

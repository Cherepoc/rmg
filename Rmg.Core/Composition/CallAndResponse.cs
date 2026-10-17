using System.Collections.Immutable;
using Rmg.Core.Events;
using Rmg.Core.Probabilities;
using Rmg.Core.Songs;

namespace Rmg.Core.Composition;

/// <summary>
///     Call and response, as a blues, soul, funk, country or Latin band answers its singer: in a verse whose phrases split in
///     two, the melody sings every phrase's first half, its last note there held to the half rather than cut, and rests in
///     the second but for its walk into the next section, where the riff answers it, or the counter-melody where no riff
///     plays, playing nothing in the first; a phrase that no part answers is left whole. A section's decision, the same wherever it
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
        // the parts that may answer, the riff before the counter-melody, by their tracks, each with its own notes, a part
        // doubling another's line no answer of its own
        int[] answerers = [..new[] { TrackRole.Riff, TrackRole.CounterMelody }.Where(song.Playing.Values.Contains).Select(x => song.Track(x).Key)];
        static bool IsOwn(TimelineItem<StateMap> note) => note.Value.GetStateValue(CompositionStateKinds.Doubled) == 0;
        // its own line, a walk into the next section none
        bool Plays(int track, double from, double to) =>
            draft.Timeline.TrackTimelineMap.TryGetValue(track, out var timeline)
            && timeline.EventTimeline.Any(x => IsOwn(x) && x.Value.GetStateValue(CompositionStateKinds.Walk) == 0 && x.IsWithin(from, to));
        var melodyTrack = song.Track(TrackRole.Melody).Key;
        // every calling appearance's phrases, each split at its half, but in a solo, and answered by the first part that
        // plays its own line in its second half; a phrase none answers, or whose melody does not call, left whole, as the
        // melody cut there would leave a gap
        var holds = draft.Holds(song);
        var halves = song.Form.Map.Sections.Where(x => calling[x.SectionId] && !holds.Meets(Hold.Solo, x.Start, x.End))
            .SelectMany(span => Enumerable.Range(0, (int)Math.Ceiling(span.Duration / span.Meter.PhraseDuration - 1e-9))
                .Select(p => span.Start + p * span.Meter.PhraseDuration)
                .Select(start => (Start: start, Half: start + span.Meter.PhraseBars / 2 * span.Meter.BarDuration, End: Math.Min(start + span.Meter.PhraseDuration, span.End))))
            .Select(x => (x.Start, x.Half, x.End, Answerer: answerers.Where(track => Plays(track, x.Half, x.End)).Select(track => (int?)track).FirstOrDefault()))
            .Where(x => x.Answerer is not null && Plays(melodyTrack, x.Start, x.Half))
            .ToArray();
        if (halves.Length == 0)
            return draft;

        // a part's notes left out where it rests, and its doubles' with them, the parts doubling it note for note
        var edits = new Dictionary<int, Func<EventTimeline<StateMap>, EventTimeline<StateMap>>>();
        void Edit(int track, Func<EventTimeline<StateMap>, EventTimeline<StateMap>> edit) =>
            edits[track] = edits.TryGetValue(track, out var before) ? notes => edit(before(notes)) : edit;
        void Rest(TrackRole role, Func<double, bool> rests, Func<EventTimeline<StateMap>, EventTimeline<StateMap>>? own = null)
        {
            Edit(song.Track(role).Key, own ?? (notes => EventTimeline.Create(notes.Duration, notes.Where(x => !IsOwn(x) || !rests(x.Position)))));
            foreach (var target in LineDoubling.Pairs.Where(x => x.Source == role && song.Playing.Values.Contains(x.Target)).Select(x => song.Track(x.Target).Key))
                Edit(target, notes => EventTimeline.Create(notes.Duration, notes.Where(x => IsOwn(x) || !rests(x.Position))));
        }

        // the melody's first half, its last note held to the half, and its walk into the next section; a part doubling it
        // holding its copy of that note as long
        var melody = draft.Timeline.TrackTimelineMap[melodyTrack].EventTimeline;
        var held = melody.Where(x => IsOwn(x) && halves.FirstOrDefault(h => x.IsWithin(h.Start, h.Half)) is { End: > 0 } phrase
                                     && !melody.Any(n => n.Position > x.Position + 1e-9 && n.IsWithin(phrase.Start, phrase.Half)))
            .ToDictionary(x => Math.Round(x.Position, 6), x => halves.First(h => x.IsWithin(h.Start, h.Half)).Half - x.Position);
        TimelineItem<StateMap> Held(TimelineItem<StateMap> note) =>
            held.TryGetValue(Math.Round(note.Position, 6), out var length) ? note.Value.With(StateKinds.HeldDuration, length).ToTimelineItem(note.Position) : note;
        Rest(TrackRole.Melody, at => halves.Any(h => at >= h.Half - 1e-9 && at < h.End - 1e-9), notes => EventTimeline.Create(notes.Duration, notes
            .Where(x => x.Value.GetStateValue(CompositionStateKinds.Walk) != 0 || !halves.Any(h => x.IsWithin(h.Half, h.End)))
            .Select(x => IsOwn(x) ? Held(x) : x)));
        foreach (var target in LineDoubling.Pairs.Where(x => x.Source == TrackRole.Melody && song.Playing.Values.Contains(x.Target)).Select(x => song.Track(x.Target).Key))
            Edit(target, notes => EventTimeline.Create(notes.Duration, notes.Select(x => IsOwn(x) ? x : Held(x))));
        // the answer, its second half, the answering part resting in the first
        foreach (var answering in halves.Select(x => x.Answerer!.Value).Distinct())
            Rest(song.Tracks.Definitions[answering].Role, at => halves.Any(h => h.Answerer == answering && at >= h.Start - 1e-9 && at < h.Half - 1e-9));
        // the halves handed on, which the stages after keep out of: stop time out of the answer, the solo part's answers too
        ImmutableArray<HeldSpan> calls = [..halves.SelectMany(x => new[] { new HeldSpan(Hold.Call, x.Start, x.Half, melodyTrack), new HeldSpan(Hold.Answer, x.Half, x.End, x.Answerer) })];
        return draft with { Timeline = draft.Timeline.MapTrackEvents(edits), Calls = calls };
    }
}

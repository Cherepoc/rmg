using System.Collections.Immutable;
using Rmg.Core.Events;
using Rmg.Core.Probabilities;
using Rmg.Core.Songs;

namespace Rmg.Core.Composition;

/// <summary>The section's pedals and the pedals leading into a key change: the bass holding a note under a phrase (<see cref="KeyChange.Pedal" />), a stage of the song after it is put together (<see cref="SongStages" />).</summary>
internal static class SectionPedals
{
    /// <summary>
    ///     The bass holding a note: a section's own pedal now and then, its home under its appearances' first phrase or its
    ///     dominant under their last, and the new key's dominant under the bar before a key change led into by a pedal,
    ///     over whatever it doubles or walks there.
    /// </summary>
    internal static SongDraft Apply(SongDecisions song, SongDraft draft)
    {
        var bass = song.Track(TrackRole.Bass);
        if (!draft.Timeline.TrackTimelineMap.TryGetValue(bass.Key, out var bassTrack))
            return draft;
        // a section's home, which the bass's state holds over the progression's roots
        int Home(double position) => bassTrack.StateTimelineMap.GetEffectiveStateMapAt(position).GetStateValue(StateKinds.ChordRoot);
        // a section's pedal now and then, by its progression facet, from a sequence of its own: its home under its
        // appearances' first phrase, or its dominant under their last, building into what follows
        var pedalling = song.SectionIds.Distinct().ToDictionary(id => id, id =>
        {
            var context = song.Stream(SongStream.SectionPedal, id);
            var pedals = context.TestProbability(Chance.At(song.SectionOf(id).Facets[Facet.Progression], song.Genre.Chance(GenreChoices.HarmonyDevices, "pedal")));
            var root = context.Pick(Roots);
            StateTrace.RecordSection(TracePoints.SectionPedal, SectionGenerator.SectionTrace, id, pedals ? $"{root}" : "none", pedals ? root : null);
            return pedals ? root : (int?)null;
        });
        // a pedal under chords that change, none under a vamp or a home held, which it would only flatten onto one note
        var common = draft.Timeline.CommonStateTimelineMap;
        // a chord's root and its move by semitones, a planed chord's
        (int, int) Root(double position)
        {
            var state = common.GetEffectiveStateMapAt(position);
            return (state.GetStateValue(StateKinds.ChordRoot), state.GetStateValue(StateKinds.ChordShift));
        }

        bool Changes(double from, double to) => common.GetStateTimeline(StateKinds.ChordChange).Any(x => x.Position > from + 1e-9 && x.Position < to - 1e-9 && Root(x.Position) != Root(from));
        var sections = song.Form.Map.Sections.Where(x => pedalling[x.SectionId] is not null)
            .Select(x => (Phrase: x.Meter.PhraseDuration, Root: pedalling[x.SectionId]!.Value, Span: x))
            .Select(x => (Start: x.Root == 0 ? x.Span.Start : x.Span.Start + Math.Floor((x.Span.Duration - 1e-9) / x.Phrase) * x.Phrase, End: x.Root == 0 ? Math.Min(x.Span.End, x.Span.Start + x.Phrase) : x.Span.End,
                Home: Home(x.Span.Start), x.Root))
            .Where(x => Changes(x.Start, x.End))
            // in the key where it starts, a key change within the section's
            .Select(x => (x.Start, x.End, x.Start, x.Home + x.Root));
        var changes = draft.Chains.Where(x => x.Device == KeyChange.Device.Pedal).Select(x => (x.Chords[0], x.Change, x.Change, 4));
        // a key change's pedal over a section's in the bar they share, as it leads into the new key
        return draft with { Timeline = KeyChange.Pedal(draft.Timeline, bass.Key, (PitchInstrumentTrack)bass.Value, sections.Concat(changes)) };
    }

    /// <summary>The chance a section holds a pedal under a phrase of its appearances, by its progression facet: never in the plainest songs.</summary>
    public static ByConvention Chance { get; } = new(0, 0.06, 0.15);

    /// <summary>A section's pedal's root, in steps above its home, and how likely: the home, a tonic pedal under its first phrase, the likelier, and the dominant under its last.</summary>
    public static ImmutableArray<Weighted<int>> Roots { get; } = [new(1, 0), new(0.6, 4)];
}

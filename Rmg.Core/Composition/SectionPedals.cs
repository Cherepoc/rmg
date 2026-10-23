using System.Collections.Immutable;
using Rmg.Core.Events;
using Rmg.Core.Probabilities;
using Rmg.Core.Songs;

namespace Rmg.Core.Composition;

/// <summary>The section's pedals and the pedals leading into a key change: the bass holding a note under a phrase (<see cref="KeyChange.Pedal" />), a stage of the song after it is put together (<see cref="SongStages" />).</summary>
internal static class SectionPedals
{
    /// <summary>
    ///     The bass holding a note: a section's own pedal now and then, its home under its appearances' first phrase, its
    ///     dominant under their last or its home under the whole of them, a drone, and the new key's dominant under the bar before a key change led into by a pedal,
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
        // appearances' first phrase, its dominant under their last, building into what follows, or a drone under the whole
        var pedalling = song.SectionIds.Distinct().ToDictionary(id => id, id =>
        {
            var context = song.Stream(SongStream.SectionPedal, id);
            var progression = song.SectionOf(id).Facets[Facet.Progression];
            var pedals = context.TestProbability(Chance.At(progression, song.Genre.Chance(GenreChoices.HarmonyDevices, "pedal")));
            var place = context.Pick(ByConvention.Weigh(Places, progression, song.Genre.Pool<PedalPlace>(GenreChoices.SectionPedal)));
            StateTrace.RecordSection(TracePoints.SectionPedal, SectionGenerator.SectionTrace, id, pedals ? $"{place}" : "none", pedals ? place : null);
            return pedals ? place : (PedalPlace?)null;
        });
        // a section that loops another's lines held as its source is, its own drawn all the same
        pedalling = pedalling.ToDictionary(x => x.Key, x => song.SectionOf(x.Key).LinesFrom is { } source ? pedalling[source] : x.Value);
        // a pedal under chords that change, none under a vamp or a home held, which it would only flatten onto one note
        var common = draft.Timeline.CommonStateTimelineMap;
        // a chord's root and its move by semitones, a planed chord's
        (int, int) Root(double position)
        {
            var state = common.GetEffectiveStateMapAt(position);
            return (state.GetStateValue(StateKinds.ChordRoot), state.GetStateValue(StateKinds.ChordShift));
        }

        bool Changes(double from, double to) => common.GetStateTimeline(StateKinds.ChordChange).Any(x => x.Position > from + 1e-9 && x.Position < to - 1e-9 && Root(x.Position) != Root(from));
        // the sections and an intro that plays as one, not the ending's final chord
        var sections = song.Form.Map.Plays.Where(x => !x.IsEnding).Select(x => x.Span).Where(x => pedalling[x.SectionId] is not null)
            .Select(x => (Place: pedalling[x.SectionId]!.Value, Span: x))
            .Select(x => (Start: x.Place == PedalPlace.Dominant ? x.Span.Phrases.Last().Start : x.Span.Start,
                End: x.Place == PedalPlace.Home ? x.Span.Phrases.First().End : x.Span.End, Home: Home(x.Span.Start), Root: x.Place == PedalPlace.Dominant ? 4 : 0))
            .Where(x => Changes(x.Start, x.End))
            // in the key where it starts, a key change within the section's
            .Select(x => (x.Start, x.End, x.Start, x.Home + x.Root));
        var changes = draft.Chains.Where(x => x.Device == KeyChange.Device.Pedal).Select(x => (x.Chords[0], x.Change, x.Change, 4));
        // a key change's pedal over a section's in the bar they share, as it leads into the new key
        return draft with { Timeline = KeyChange.Pedal(draft.Timeline, bass.Key, (PitchInstrumentTrack)bass.Value, sections.Concat(changes)) };
    }

    /// <summary>The chance a section holds a pedal under a phrase of its appearances, by its progression facet: never in the plainest songs.</summary>
    public static ByConvention Chance { get; } = new(0, 0.06, 0.15);

    /// <summary>
    ///     Where a section holds its pedal, and how likely: its home under its first phrase, the likelier, its dominant under its
    ///     last, and its home under the whole of it, a drone, a genre's alone.
    /// </summary>
    public static ImmutableArray<(PedalPlace Place, ByConvention Weight)> Places { get; } =
        [(PedalPlace.Home, new(1, 1, 1)), (PedalPlace.Dominant, new(0.6, 0.6, 0.6)), (PedalPlace.Drone, new(0, 0, 0))];

    /// <summary>A place as a genre's page shows it.</summary>
    public static string LabelOf(PedalPlace place) => place switch
    {
        PedalPlace.Home => "Its home under its first phrase",
        PedalPlace.Dominant => "Its dominant under its last phrase",
        PedalPlace.Drone => "Its home under the whole of it, a drone",
        _ => throw new ArgumentOutOfRangeException(nameof(place), place, null)
    };
}

/// <summary>Where a section holds its pedal (<see cref="SectionPedals.Places" />).</summary>
public enum PedalPlace
{
    Home,
    Dominant,
    Drone
}

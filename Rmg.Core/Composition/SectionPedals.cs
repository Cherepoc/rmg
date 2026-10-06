using System.Collections.Immutable;
using Rmg.Core.Events;
using Rmg.Core.Probabilities;
using Rmg.Core.Songs;

namespace Rmg.Core.Composition;

/// <summary>The section's pedals and the pedals leading into a key change: the bass holding a note under a phrase (<see cref="KeyChange.Pedal" />), a stage of the song after it is put together (<see cref="SongStages" />).</summary>
internal static class SectionPedals
{
    /// <summary>The bass holding the new key's dominant under the bar before a key change led into by a pedal, over whatever it doubles or walks there.</summary>
    internal static SongDraft Apply(SongDecisions song, SongDraft draft)
    {
        var bass = song.Track(TrackRole.Bass);
        var common = draft.Timeline.CommonStateTimelineMap;
        // a section's pedal now and then, its home or its dominant under its appearances' first phrase, by its progression
        // facet, from a sequence of its own
        var pedalling = song.SectionIds.Distinct().ToDictionary(id => id, id =>
        {
            var context = song.Stream(SongStream.SectionPedal, id);
            var pedals = context.TestProbability(Chance.At(song.SectionOf(id).Facets[Facet.Progression], song.Genre.Chance(GenreChoices.HarmonyDevices, "pedal")));
            var root = context.Pick(Roots);
            StateTrace.Record(TracePoints.SectionPedal, SectionGenerator.SectionTrace, id, 0, StateMap.Default, 0, pedals ? $"{root}" : "none", pedals ? root : null);
            return pedals ? root : (int?)null;
        });
        var sections = song.Form.Map.Sections.Where(x => pedalling[x.SectionId] is not null)
            .Select(x => (x.Start, Math.Min(x.End, x.Start + Meter.PhraseBarCount * x.Meter.BarDuration), x.Start,
                common.GetEffectiveStateMapAt(x.Start).GetStateValue(StateKinds.ChordRoot) + pedalling[x.SectionId]!.Value));
        var changes = draft.Chains.Where(x => x.Device == KeyChange.Device.Pedal).Select(x => (x.Chords[0], x.Change, x.Change, 4));
        return draft with { Timeline = KeyChange.Pedal(draft.Timeline, bass.Key, (PitchInstrumentTrack)bass.Value, changes.Concat(sections)) };
    }

    /// <summary>The chance a section holds a pedal under its appearances' first phrase, by its progression facet: never in the plainest songs.</summary>
    public static ByConvention Chance { get; } = new(0, 0.06, 0.15);

    /// <summary>A section's pedal's root, in steps above its home, and how likely: the home, a tonic pedal, the likelier, and the dominant.</summary>
    public static ImmutableArray<Weighted<int>> Roots { get; } = [new(1, 0), new(0.6, 4)];
}

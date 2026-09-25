using Rmg.Core.Composition;
using Rmg.Core.Events;
using Rmg.Core.Songs;

namespace Rmg.Tests.SongGenerators;

/// <summary>
///     Which chord note a track plays: a value for the whole bar pattern, where the pattern starts in the chord, plus
///     the note-to-note walk. The chord track plays whole chords, so it has none.
/// </summary>
public sealed class SongGeneratorChordNoteTest
{
    private const int ChordTrackNumber = 4;
    private const int MelodyTrackNumber = 5;
    private const int BassTrackNumber = 6;

    // a bar pattern is one bar long
    private const double PatternDuration = 4;

    [Test]
    public async Task ChordTrack_HasNoChordNoteOffset()
    {
        for (var seed = 0; seed < 10; seed++)
        {
            foreach (var (position, state) in GetNotes(SongGenerator.GenerateSong(seed), ChordTrackNumber))
            {
                await Assert.That(state.GetStateValue(StateKinds.ChordNoteOffset).IsEmpty)
                    .IsTrue()
                    .Because($"seed {seed}, position {position}");
            }
        }
    }

    [Test]
    [Arguments(MelodyTrackNumber)]
    [Arguments(BassTrackNumber)]
    public async Task PitchedTrack_HasAChordNoteOffsetForEachBarPattern(int trackNumber)
    {
        for (var seed = 0; seed < 10; seed++)
        {
            var notes = GetNotes(SongGenerator.GenerateSong(seed), trackNumber);
            foreach (var pattern in notes.GroupBy(x => Math.Floor(x.Position / PatternDuration)))
            {
                var offsets = pattern.Select(x => x.State.GetStateValue(StateKinds.ChordNoteOffset)).ToList();
                var because = $"seed {seed}, track {trackNumber}, bar {pattern.Key}";

                // the pattern's value and the note's own
                foreach (var noteOffsets in offsets)
                    await Assert.That(noteOffsets.Length).IsEqualTo(2).Because(because);

                var shared = offsets.Skip(1).Aggregate(
                    offsets[0].ToHashSet(),
                    (common, noteOffsets) => common.Intersect(noteOffsets).ToHashSet()
                );
                await Assert.That(shared.Count).IsGreaterThanOrEqualTo(1).Because(because);
            }
        }
    }

    private static List<(double Position, StateMap State)> GetNotes(Song song, int trackNumber)
    {
        // the same merge as Render does
        return song.TrackEventStateTimelineMap.TrackTimelineMap[trackNumber]
            .MergeStateMap(song.TrackDefinitions[trackNumber].StateMap)
            .MergeStateTimelineMap(song.TrackEventStateTimelineMap.CommonStateTimelineMap)
            .ToMappedEventTimeline((s1, s2) => StateMap.Aggregate([s1, s2]))
            .Select(x => (x.Position, x.Value))
            .ToList();
    }
}

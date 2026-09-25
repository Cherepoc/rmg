using Rmg.Core.Composition;
using Rmg.Core.Events;
using Rmg.Core.Songs;

namespace Rmg.Tests.SongGenerators;

/// <summary>
///     The chord progression changes the chord by bar: its root and its shape (which scale steps it has), the same for
///     every pitched track.
/// </summary>
public sealed class SongGeneratorChordProgressionTest
{
    // a section is a 4-bar pattern played twice
    private const double SectionDuration = 32;

    [Test]
    public async Task ChordShape_ChangesWithinASection()
    {
        var changes = 0;
        for (var seed = 0; seed < 10; seed++)
        {
            var song = SongGenerator.GenerateSong(seed);
            changes += GetPitchedNotes(song)
                .GroupBy(x => (x.TrackNumber, Section: Math.Floor(x.Position / SectionDuration)))
                .Count(x => x.Select(note => note.Shape).Distinct().Count() > 1);
        }

        await Assert.That(changes).IsGreaterThan(0);
    }

    [Test]
    public async Task PitchedTracks_ShareTheChordShapeAtTheSameTime()
    {
        for (var seed = 0; seed < 10; seed++)
        {
            var song = SongGenerator.GenerateSong(seed);
            foreach (var notesAtPosition in GetPitchedNotes(song).GroupBy(x => x.Position))
            {
                await Assert.That(notesAtPosition.Select(x => x.Shape).Distinct().Count())
                    .IsEqualTo(1)
                    .Because($"seed {seed}, position {notesAtPosition.Key}");
            }
        }
    }

    [Test]
    public async Task ChordTrack_PlaysTheProgressionRoot()
    {
        // the chord track does not move its root from note to note, so its root is only the progression's: the
        // section's offset, the same for the whole section, and the offset of the bar it plays in
        const int chordTrackNumber = 4;
        for (var seed = 0; seed < 10; seed++)
        {
            var song = SongGenerator.GenerateSong(seed);
            var commonStateTimelineMap = song.TrackEventStateTimelineMap.CommonStateTimelineMap;
            var notes = song.TrackEventStateTimelineMap.TrackTimelineMap[chordTrackNumber]
                .MergeStateMap(song.TrackDefinitions[chordTrackNumber].StateMap)
                .MergeStateTimelineMap(commonStateTimelineMap)
                .ToMappedEventTimeline((s1, s2) => StateMap.Aggregate([s1, s2]));

            foreach (var section in notes.GroupBy(x => Math.Floor(x.Position / SectionDuration)))
            {
                var sectionOffsets = new HashSet<double>();
                foreach (var note in section)
                {
                    var offsets = note.Value.GetStateValue(StateKinds.ChordRootNoteOffset).ToList();
                    var barOffsets = commonStateTimelineMap.GetEffectiveStateMapAt(note.Position)
                        .GetStateValue(StateKinds.ChordRootNoteOffset);
                    foreach (var barOffset in barOffsets)
                        offsets.Remove(barOffset);

                    await Assert.That(offsets.Count)
                        .IsEqualTo(1)
                        .Because($"seed {seed}, position {note.Position}: only the section's offset is left");
                    sectionOffsets.Add(offsets[0]);
                }

                await Assert.That(sectionOffsets.Count)
                    .IsEqualTo(1)
                    .Because($"seed {seed}, section {section.Key}: the section's offset is the same all along");
            }
        }
    }

    [Test]
    public async Task DefaultSteps_ChangeTheShapeOnlyAtBarLines()
    {
        for (var seed = 0; seed < 10; seed++)
        {
            var song = SongGenerator.GenerateSong(seed);
            foreach (var bar in GetPitchedNotes(song).GroupBy(x => (x.TrackNumber, Bar: Math.Floor(x.Position / 4))))
            {
                await Assert.That(bar.Select(x => x.Shape).Distinct().Count())
                    .IsEqualTo(1)
                    .Because($"seed {seed}, track {bar.Key.TrackNumber}, bar {bar.Key.Bar}");
            }
        }
    }

    [Test]
    public async Task HalfBarShapeSteps_ChangeTheShapeWithinABar()
    {
        var settings = ProgressionSettings.Default with { ChordShapeStep = 2 };
        var changesWithinABar = 0;
        for (var seed = 0; seed < 10; seed++)
        {
            var notes = GetPitchedNotes(SongGenerator.GenerateSong(seed, settings)).ToList();

            // the shape still only changes at half-bar lines
            foreach (var halfBar in notes.GroupBy(x => (x.TrackNumber, HalfBar: Math.Floor(x.Position / 2))))
            {
                await Assert.That(halfBar.Select(x => x.Shape).Distinct().Count())
                    .IsEqualTo(1)
                    .Because($"seed {seed}, track {halfBar.Key.TrackNumber}, half bar {halfBar.Key.HalfBar}");
            }

            changesWithinABar += notes
                .GroupBy(x => (x.TrackNumber, Bar: Math.Floor(x.Position / 4)))
                .Count(x => x.Select(note => note.Shape).Distinct().Count() > 1);
        }

        await Assert.That(changesWithinABar).IsGreaterThan(0);
    }

    [Test]
    public async Task HalfBarShapeSteps_PitchedTracksShareTheChordShapeAtTheSameTime()
    {
        var settings = ProgressionSettings.Default with { ChordShapeStep = 2 };
        for (var seed = 0; seed < 10; seed++)
        {
            foreach (var notesAtPosition in GetPitchedNotes(SongGenerator.GenerateSong(seed, settings)).GroupBy(x => x.Position))
            {
                await Assert.That(notesAtPosition.Select(x => x.Shape).Distinct().Count())
                    .IsEqualTo(1)
                    .Because($"seed {seed}, position {notesAtPosition.Key}");
            }
        }
    }

    private static IEnumerable<(int TrackNumber, double Position, string Shape)> GetPitchedNotes(Song song)
    {
        var commonStateTimelineMap = song.TrackEventStateTimelineMap.CommonStateTimelineMap;
        foreach (var (trackNumber, trackTimelineMap) in song.TrackEventStateTimelineMap.TrackTimelineMap)
        {
            if (song.TrackDefinitions[trackNumber] is not PitchInstrumentTrack)
                continue;

            // the same merge as Render does
            var notes = trackTimelineMap
                .MergeStateMap(song.TrackDefinitions[trackNumber].StateMap)
                .MergeStateTimelineMap(commonStateTimelineMap)
                .ToMappedEventTimeline((s1, s2) => StateMap.Aggregate([s1, s2]));
            foreach (var note in notes)
            {
                var shape = string.Join(",", note.Value.GetStateValue(StateKinds.ChordNotePitchOffsets));
                yield return (trackNumber, note.Position, shape);
            }
        }
    }
}

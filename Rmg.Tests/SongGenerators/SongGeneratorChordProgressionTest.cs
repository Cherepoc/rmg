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
    [Test]
    public async Task ChordShape_ChangesWithinASection()
    {
        var changes = 0;
        for (var seed = 0; seed < 8; seed++)
        {
            var song = TestCorpus.Get(seed).Song;
            // a section is a 4-bar pattern played twice
            changes += GetPitchedNotes(song)
                .GroupBy(x => (x.TrackNumber, Section: Math.Floor(x.Position / (2 * song.Map!.Meter.PatternDuration))))
                .Count(x => x.Select(note => note.Shape).Distinct().Count() > 1);
        }

        await Assert.That(changes).IsGreaterThan(0);
    }

    [Test]
    public async Task PitchedTracks_ShareTheChordShapeAtTheSameTime()
    {
        for (var seed = 0; seed < 8; seed++)
        {
            var song = TestCorpus.Get(seed).Song;
            // a note with no shape, as a bass's walk into a section, plays its step, not a chord
            foreach (var notesAtPosition in GetPitchedNotes(song).Where(x => x.Shape != "").GroupBy(x => x.Position))
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
        for (var seed = 0; seed < 8; seed++)
        {
            var corpusSong = TestCorpus.Get(seed);
            var song = corpusSong.Song;
            var commonStateTimelineMap = song.TrackEventStateTimelineMap.CommonStateTimelineMap;
            var notes = song.TrackEventStateTimelineMap.TrackTimelineMap[chordTrackNumber]
                .MergeStateMap(song.TrackDefinitions[chordTrackNumber].StateMap)
                .MergeStateTimelineMap(commonStateTimelineMap)
                .ToMappedEventTimeline((s1, s2) => StateMap.Aggregate([s1, s2]));

            foreach (var section in notes.GroupBy(x => corpusSong.Map.Sections.LastOrDefault(s => s.Start <= x.Position + 1e-9)?.Start))
            {
                var sectionOffsets = new HashSet<int>();
                foreach (var note in section)
                {
                    // the note's root less the bar's, the progression's, leaves the section's home
                    var barRoot = commonStateTimelineMap.GetEffectiveStateMapAt(note.Position).GetStateValue(StateKinds.ChordRoot);
                    sectionOffsets.Add(note.Value.GetStateValue(StateKinds.ChordRoot) - barRoot);
                }

                await Assert.That(sectionOffsets.Count)
                    .IsEqualTo(1)
                    .Because($"seed {seed}, section {section.Key}: the section's offset is the same all along");
            }
        }
    }

    [Test]
    public async Task DefaultSteps_ChangeTheShapeOnlyAtBarLinesAndChordChanges()
    {
        for (var seed = 0; seed < 8; seed++)
        {
            var corpusSong = TestCorpus.Get(seed);
            var changes = corpusSong.ChordChanges;
            int Chord(double position) => Array.FindLastIndex(changes, x => x <= position + 1e-9);
            foreach (var bar in GetPitchedNotes(corpusSong.Song).Where(x => x.Shape != "").GroupBy(x => (x.TrackNumber, Bar: Math.Floor(x.Position / corpusSong.Map.Meter.BarDuration), Chord: Chord(x.Position))))
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
        var settings = ProgressionSettings.Default with { ChordShapeStep = 0.5 };
        var changesWithinABar = 0;
        for (var seed = 0; seed < 8; seed++)
        {
            var song = SongGenerator.GenerateSong((ulong)seed, settings);
            var bar = song.Map!.Meter.BarDuration;
            var notes = GetPitchedNotes(song).Where(x => x.Shape != "").ToList();

            // the shape still only changes at half-bar lines
            // counted in the bars of the note's section
            double HalfBarOf(double position) => song.Map.SectionAt(position) is { } span ? span.Start * 1000 + Math.Floor((position - span.Start) / (span.Meter.BarDuration / 2)) : Math.Floor(position / (bar / 2));
            foreach (var halfBar in notes.GroupBy(x => (x.TrackNumber, HalfBar: HalfBarOf(x.Position))))
            {
                await Assert.That(halfBar.Select(x => x.Shape).Distinct().Count())
                    .IsEqualTo(1)
                    .Because($"seed {seed}, track {halfBar.Key.TrackNumber}, half bar {halfBar.Key.HalfBar}");
            }

            changesWithinABar += notes
                .GroupBy(x => (x.TrackNumber, Bar: Math.Floor(x.Position / bar)))
                .Count(x => x.Select(note => note.Shape).Distinct().Count() > 1);
        }

        await Assert.That(changesWithinABar).IsGreaterThan(0);
    }

    [Test]
    public async Task HalfBarShapeSteps_PitchedTracksShareTheChordShapeAtTheSameTime()
    {
        var settings = ProgressionSettings.Default with { ChordShapeStep = 0.5 };
        for (var seed = 0; seed < 8; seed++)
        {
            foreach (var notesAtPosition in GetPitchedNotes(SongGenerator.GenerateSong((ulong)seed, settings)).Where(x => x.Shape != "").GroupBy(x => x.Position))
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

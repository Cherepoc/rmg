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
            var corpus = TestCorpus.Get(seed);
            // by the section each note is in
            changes += GetPitchedNotes(corpus.Song)
                .GroupBy(x => (x.TrackNumber, Section: corpus.Map.SectionAt(x.Position)?.Start))
                .Count(x => x.Select(note => note.Shape).Distinct().Count() > 1);
        }

        await Assert.That(changes).IsGreaterThan(0);
    }

    [Test]
    [Arguments(1)]
    [Arguments(3)]
    public async Task PitchedTracks_ShareTheChordShapeAtTheSameTime(int phrases)
    {
        for (var seed = 0; seed < 64; seed++)
        {
            // a section's pattern of a phrase, as every song's, or of three, the blues' twelve bars
            var song = (phrases == 1 ? TestCorpus.Get(seed) : TestCorpus.Get(seed, new SongOverrides(Genre: TestCorpus.PhrasesOnly(phrases)))).Song;
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
        for (var seed = 0; seed < 8; seed++)
        {
            var corpusSong = TestCorpus.Get(seed);
            var song = corpusSong.Song;
            var commonStateTimelineMap = song.TrackEventStateTimelineMap.CommonStateTimelineMap;
            var notes = Realizer.GetNoteStates(song.TrackEventStateTimelineMap.TrackTimelineMap[song.Track(TrackRole.Chords)], song.TrackDefinitions[song.Track(TrackRole.Chords)], commonStateTimelineMap);

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
    [Arguments(1)]
    [Arguments(3)]
    public async Task DefaultSteps_ChangeTheShapeOnlyAtBarLinesAndChordChanges(int phrases)
    {
        for (var seed = 0; seed < 8; seed++)
        {
            var corpusSong = phrases == 1 ? TestCorpus.Get(seed) : TestCorpus.Get(seed, new SongOverrides(Genre: TestCorpus.PhrasesOnly(phrases)));
            var changes = corpusSong.ChordChanges;
            int Chord(double position) => Array.FindLastIndex(changes, x => x <= position + 1e-9);
            // counted in the bars of the note's section, whose meter may be its own
            double BarOf(double position) => corpusSong.Map.SectionAt(position) is { } span
                ? span.Start * 1000 + Math.Floor((position - span.Start) / span.Meter.BarDuration)
                : Math.Floor(position / corpusSong.Map.Meter.BarDuration);
            foreach (var bar in GetPitchedNotes(corpusSong.Song).Where(x => x.Shape != "").GroupBy(x => (x.TrackNumber, Bar: BarOf(x.Position), Chord: Chord(x.Position))))
            {
                await Assert.That(bar.Select(x => x.Shape).Distinct().Count())
                    .IsEqualTo(1)
                    .Because($"seed {seed}, track {bar.Key.TrackNumber}, bar {bar.Key.Bar}");
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

            var notes = Realizer.GetNoteStates(trackTimelineMap, song.TrackDefinitions[trackNumber], commonStateTimelineMap);
            foreach (var note in notes)
            {
                // but a solo's, whose stacked notes the soloist voices as it stacks them, an octave on a power chord, and the solo
                // part's answers to the melody, lines of the same vocabulary
                if (note.Value.GetStateValue(CompositionStateKinds.LineSolo) == 1 || song.TrackDefinitions[trackNumber].Role == TrackRole.Solo)
                    continue;
                var shape = string.Join(",", note.Value.GetStateValue(StateKinds.ChordNotePitchOffsets));
                yield return (trackNumber, note.Position, shape);
            }
        }
    }
}

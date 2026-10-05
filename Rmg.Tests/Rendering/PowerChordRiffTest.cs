using Rmg.Core.Composition;
using Rmg.Core.Songs;
using Rmg.Tests.Genres;

namespace Rmg.Tests.Rendering;

/// <summary>A riff in power chords: every note with its perfect fifth over it, whatever the scale, and no twin.</summary>
public sealed class PowerChordRiffTest
{
    [Test]
    public async Task ARiffInPowerChords_PlaysItsFifthOverEveryNote_AndNoTwin()
    {
        int notes = 0, fifths = 0, twinNotes = 0, sections = 0;
        foreach (var song in TestCorpus.InParallel(Enumerable.Range(0, 16), seed => TestCorpus.Get(seed, new SongOverrides(Genre: GenrePresets.Genre("rock")))))
        {
            var power = song.Trace.Where(x => x.Point == TracePoints.RiffPowerChords && (bool)x.Value!).Select(x => x.Section).ToHashSet();
            foreach (var span in song.Map.Sections.Where(x => power.Contains(x.SectionId)))
            {
                sections++;
                var riff = song.Song.Notes!.GetValueOrDefault(SongTracks.RiffTrack)?.Where(x => x.Position >= span.Start && x.Position < span.End).ToArray() ?? [];
                foreach (var note in riff)
                {
                    notes++;
                    fifths += note.Value.Pitches.Length > 1 && note.Value.Pitches.Contains(note.Value.Pitches.Min() + 7) ? 1 : 0;
                }
                twinNotes += song.Song.Notes!.GetValueOrDefault(SongTracks.RiffTwinTrack)?.Count(x => x.Position >= span.Start && x.Position < span.End) ?? 0;
            }
        }

        await Assert.That(sections).IsGreaterThan(5);
        // but where a section's landing stacks the band's chord (LineChords.Riff), or the range has no room above the note:
        // 86% (October 2026)
        await Assert.That(fifths / (double)notes).IsGreaterThan(0.8);
        await Assert.That(twinNotes).IsEqualTo(0);
    }
}

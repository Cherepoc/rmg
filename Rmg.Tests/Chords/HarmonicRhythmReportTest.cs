using Rmg.Core.Composition;

namespace Rmg.Tests.Chords;

/// <summary>
///     How often the sections' chords change: the spans drawn, by the sections' energy, and how often a pitched note sounds
///     on across a change of chord, playing over the new chord what it chose for the old.
/// </summary>
public sealed class HarmonicRhythmReportTest
{
    [Test]
    public async Task EverySection_ChangesItsChordsWhereItsSpanSays()
    {
        foreach (var song in TestCorpus.Range(20))
        {
            var spans = song.Trace.Where(x => x.Point == TracePoints.HarmonicRhythm).Select(x => ((HarmonicRhythm)x.Value!).Span).ToArray();
            await Assert.That(spans.All(x => x is 2 or 4 or 8)).IsTrue();

            var changes = song.ChordChanges;

            var gaps = changes.Zip(changes.Skip(1), (a, b) => b - a).ToArray();
            await Assert.That(gaps.All(x => x > 0)).IsTrue();
        }
    }

    [Test]
    public async Task TheChordsAndTheBass_StrikeMostChanges_InTheirRange()
    {
        var (played, struck) = (0, 0);
        foreach (var song in TestCorpus.Range(20))
        foreach (var track in new[] { SongTracks.ChordsTrack, SongTracks.BassTrack })
        {
            var notes = song.Song.Notes![track];
            var (low, high) = Realizer.GetRange((Rmg.Core.Songs.PitchInstrumentTrack)song.Song.TrackDefinitions[track]);
            await Assert.That(notes.All(x => x.Value.Pitches.All(p => p >= low - 12 && p <= high + 12))).IsTrue().Because($"seed {song.Seed}, track {track}");

            var onsets = notes.Select(x => Math.Round(x.Position, 6)).ToHashSet();
            var through = song.ChordChanges.Where(x => x > notes[0].Position && x < notes[^1].Position).ToArray();
            (played, struck) = (played + through.Length, struck + through.Count(x => onsets.Contains(Math.Round(x, 6))));
        }

        await Assert.That(struck / (double)played).IsGreaterThan(0.8);
    }

    [Test]
    [Explicit]
    public async Task Report()
    {
        var sections = new List<(double Span, double Energy)>();
        var notes = new Dictionary<int, (int All, int Crossing)>();
        var struckChanges = new Dictionary<int, (int All, int Struck)>();
        foreach (var song in TestCorpus.Range(200))
        {
            var energies = song.Trace.Where(x => x.Point == TracePoints.SectionEnergy)
                .ToDictionary(x => x.Section, x => ((SectionEnergyTrace)x.Value!).Energy);
            sections.AddRange(song.Trace.Where(x => x.Point == TracePoints.HarmonicRhythm)
                .Select(x => (((HarmonicRhythm)x.Value!).Span, energies[x.Section])));

            var changes = song.ChordChanges;
            foreach (var track in new[] { SongTracks.ChordsTrack, SongTracks.BassTrack })
            {
                var onsets = song.Song.Notes![track].Select(x => Math.Round(x.Position, 6)).ToHashSet();
                // the changes the track plays through, from its first note to its last
                var played = changes.Where(x => x > song.Song.Notes[track][0].Position && x < song.Song.Notes[track][^1].Position).ToArray();
                var (all, struck) = struckChanges.GetValueOrDefault(track);
                struckChanges[track] = (all + played.Length, struck + played.Count(x => onsets.Contains(Math.Round(x, 6))));
            }
            foreach (var track in new[] { SongTracks.ChordsTrack, SongTracks.BassTrack, SongTracks.MelodyTrack })
            foreach (var note in song.Song.Notes![track])
            {
                var end = note.Position + note.Value.Duration;
                var (all, crossing) = notes.GetValueOrDefault(track);
                notes[track] = (all + 1, crossing + (changes.Any(x => x > note.Position + 1e-9 && x < end - 1e-9) ? 1 : 0));
            }
        }

        foreach (var span in sections.GroupBy(x => x.Span).OrderBy(x => x.Key))
            Console.WriteLine($"span {span.Key}: {span.Count()} sections ({span.Count() / (double)sections.Count:P0}), energy {span.Average(x => x.Energy):F2} on average");
        foreach (var (track, (all, struck)) in struckChanges)
            Console.WriteLine($"track {track} strikes {struck / (double)all:P1} of {all} changes of chord it plays through");
        foreach (var (track, (all, crossing)) in notes)
            Console.WriteLine($"track {track}'s notes sounding across a change: {crossing / (double)all:P1} of {all}");
        await Task.CompletedTask;
    }
}

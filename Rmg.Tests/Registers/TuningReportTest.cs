using Rmg.Core.Composition;
using Rmg.Core.Songs;
using Rmg.Tests.Genres;

namespace Rmg.Tests.Registers;

/// <summary>Where the metal family's guitars sit against their instruments' lowest note, a tuning's floor.</summary>
public sealed class TuningReportTest
{
    /// <summary>
    ///     Each metal preset's riff, rhythm part and bass: their notes' lowest pitch, the median and the lowest tenth, and how
    ///     many of their notes sit within a tone of their instrument's lowest note, as a band tuned down plays its open string.
    /// </summary>
    [Test]
    [Explicit]
    public async Task Report()
    {
        foreach (var genre in new[] { "metal", "thrash", "doom", "nuMetal", "djent", "blackMetal" })
        {
            var songs = TestCorpus.InParallel(Enumerable.Range(0, 64), seed => TestCorpus.Get(seed, new SongOverrides(Genre: GenrePresets.Genre(genre)))).ToArray();
            var line = new List<string>();
            foreach (var role in new[] { TrackRole.Riff, TrackRole.Rhythm, TrackRole.Bass })
            {
                var (lows, floor, all) = (new List<int>(), 0, 0);
                foreach (var song in songs.Where(x => x.Song.TrackDefinitions.Values.Any(t => t.Role == role)))
                {
                    var part = (PitchInstrumentTrack)song.Song.TrackDefinitions[song.Track(role)];
                    foreach (var note in song.Song.Notes!.GetValueOrDefault(song.Track(role))?.ToArray() ?? [])
                    {
                        var low = note.Value.Pitches.Min();
                        lows.Add(low);
                        all++;
                        // on the part's own instrument as it tunes it
                        var program = InstrumentsOverTime.ProgramOf(note.Value.State, part.InstrumentCode);
                        floor += low - (program == part.InstrumentCode ? Rmg.Core.Composition.Registers.RangeOf(part) : InstrumentRanges.Of(program)).Low <= 2 ? 1 : 0;
                    }
                }
                lows.Sort();
                // the floors of the parts' ranges, the lowest note each may play
                var floors = songs.Where(x => x.Song.TrackDefinitions.Values.Any(t => t.Role == role))
                    .Select(x => Realizer.GetRange((PitchInstrumentTrack)x.Song.TrackDefinitions[x.Track(role)]).Low).CountBy(x => x).OrderBy(x => x.Key);
                line.Add(lows.Count == 0 ? $"{role} none" : $"{role} median {lows[lows.Count / 2]} lowest tenth {lows[lows.Count / 10]} at the floor {floor / (double)all:P0}, floors {string.Join(" ", floors.Select(x => $"{x.Key}:{x.Value}"))}");
            }
            Console.WriteLine($"{genre}: {string.Join("; ", line)}");
        }

        await Task.CompletedTask;
    }
}

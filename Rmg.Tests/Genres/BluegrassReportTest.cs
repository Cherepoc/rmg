using Rmg.Core.Composition;
using Rmg.Core.Songs;

namespace Rmg.Tests.Genres;

/// <summary>How Bluegrass's songs are shaped: their breaks against their sung sections, the voice's instrument and the breaks', the banjos, the melody's notes and the forms.</summary>
public sealed class BluegrassReportTest
{
    [Test]
    [Explicit]
    public async Task Report()
    {
        var songs = TestCorpus.InParallel(Enumerable.Range(0, 64), seed => TestCorpus.Get(seed, new SongOverrides(Genre: GenrePresets.Genre("bluegrass"))));
        int Instrument(CorpusSong song, TrackRole role) => song.Song.TrackDefinitions.Values.FirstOrDefault(x => x.Role == role) is PitchInstrumentTrack p ? p.InstrumentCode : -1;
        string Name(int code) => code < 0 ? "none" : GeneralMidi.Programs[code];

        var later = songs.Sum(x => x.Map.Sections.Length - 1);
        var breaks = songs.Sum(x => x.Solos.Count);
        var outnumbered = songs.Count(x => x.Solos.Count > x.Map.Sections.Length - x.Solos.Count);
        Console.WriteLine($"  breaks {breaks} of {later} sections after the first ({breaks / (double)later:P0}); songs with more breaks than sung sections {outnumbered} of 64");

        var pitched = songs.SelectMany(x => x.Solos.Values.Where(s => !s.IsDrumSolo).Select(s => (Song: x, Soloist: s.Soloists[0]))).ToArray();
        Console.WriteLine($"  the melody on {string.Join(", ", songs.GroupBy(x => Instrument(x, TrackRole.Melody)).OrderByDescending(x => x.Count()).Select(x => $"{Name(x.Key)} {x.Count()}"))}; " +
                          $"breaks on {string.Join(", ", pitched.GroupBy(x => Instrument(x.Song, x.Soloist)).OrderByDescending(x => x.Count()).Select(x => $"{Name(x.Key)} {x.Count()}"))}; " +
                          $"on the melody's instrument {pitched.Count(x => Instrument(x.Song, x.Soloist) == Instrument(x.Song, TrackRole.Melody))} of {pitched.Length}");
        var banjos = songs.Select(x => x.Song.TrackDefinitions.Values.Count(d => d is PitchInstrumentTrack { InstrumentCode: 105 })).ToArray();
        Console.WriteLine($"  banjos a song {banjos.Average():F1}, songs of more than one {banjos.Count(x => x > 1)} of 64");

        var density = songs.SelectMany(x => x.Map.Sections.Select((s, i) => (s, i)).Where(y => !x.Solos.ContainsKey(y.i))
                .Select(y => (x.Song.Notes!.GetValueOrDefault(x.Track(TrackRole.Melody))?.Count(n => n.IsWithin(y.s.Start, y.s.End)) ?? 0) / (y.s.Duration / y.s.Meter.BarDuration)))
            .Where(x => x > 0).Order().ToArray();
        Console.WriteLine($"  the melody's notes a bar where it sings: median {density[density.Length / 2]:F1}, lowest quarter {density[density.Length / 4]:F1}");

        var forms = songs.Select(x => (SongStructure)x.Trace.Single(t => t.Point == TracePoints.SongForm).Value!).ToArray();
        Console.WriteLine($"  songs with a bridge {forms.Count(x => x.Roles.Values.Contains(SectionRole.Bridge))} of 64, with a chorus {forms.Count(x => x.Roles.Values.Contains(SectionRole.Chorus))}");
        await Task.CompletedTask;
    }
}

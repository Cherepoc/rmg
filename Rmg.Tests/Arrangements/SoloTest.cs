using System.Collections.Immutable;
using Rmg.Core.Composition;
using Rmg.Core.Probabilities;
using Rmg.Core.Songs;

namespace Rmg.Tests.Arrangements;

public sealed class SoloTest
{
    private static readonly TrackRole[] Band = [TrackRole.Melody, TrackRole.Chords, TrackRole.Bass, TrackRole.Riff, TrackRole.Drum];

    [Test]
    public async Task ThePlainestSongs_HaveNoSolo_AndTheWildestOneAsLikelyAsNot()
    {
        var context = new GenerationContext(1);
        var plain = Unconventionality.Generate(0, _ => context);
        var wild = Unconventionality.Generate(1, _ => context);

        await Assert.That(Enumerable.Range(0, 1000).All(_ => Solos.Draw(context, 1, plain, Band) is null)).IsTrue();
        var solos = Enumerable.Range(0, 4000).Count(_ => Solos.Draw(context, 1, wild, Band) is not null) / 4000.0;
        await Assert.That(solos).IsEqualTo(0.5).Within(0.03);
        // and never the song's first section
        await Assert.That(Enumerable.Range(0, 1000).All(_ => Solos.Draw(context, 0, wild, Band) is null)).IsTrue();
    }

    [Test]
    public async Task ASolo_IsPlayedByItsSoloists_TheMelodyResting_AndADrumSolo_ByTheDrumsAlone()
    {
        var checkedSolos = 0;
        foreach (var song in TestCorpus.InParallel(Enumerable.Range(0, 64), seed => TestCorpus.Get(seed, new SongOverrides(Facets: ImmutableDictionary<Facet, double>.Empty.Add(Facet.Form, 1)))))
        foreach (var (index, solo) in song.Solos)
        {
            var span = song.Map.Sections[index];
            int Notes(TrackRole role) => song.Song.Notes!.Where(x => song.Song.TrackDefinitions[x.Key].Role == role)
                .Sum(x => x.Value.Count(n => n.Position >= span.Start + 1e-9 && n.Position < span.End - song.Map.Meter.BarDuration));
            if (solo.IsDrumSolo)
            {
                foreach (var part in Enum.GetValues<TrackRole>().Where(x => x != TrackRole.Drum))
                    await Assert.That(Notes(part)).IsEqualTo(0).Because($"seed {song.Seed}, {part} in a drum solo");
            }
            else
            {
                if (!solo.Soloists.Contains(TrackRole.Melody))
                    await Assert.That(Notes(TrackRole.Melody)).IsEqualTo(0).Because($"seed {song.Seed}, the melody in a solo");
                await Assert.That(solo.Soloists.Sum(Notes)).IsGreaterThan(0).Because($"seed {song.Seed}, the soloists");
            }

            checkedSolos++;
        }

        await Assert.That(checkedSolos).IsGreaterThan(10);
    }

    [Test]
    [Explicit]
    public async Task Report()
    {
        var songs = TestCorpus.Range(256);
        foreach (var band in songs.GroupBy(x => Math.Clamp((int)(((Unconventionality)x.Trace.Single(t => t.Point == TracePoints.SongUnconventionality).Value!)[Facet.Form] * 5), 0, 4)).OrderBy(x => x.Key))
            Console.WriteLine($"  form {band.Key}/5, {band.Count()} songs: with a solo {band.Count(x => !x.Solos.IsEmpty) / (double)band.Count():P0}, solos a song {band.Average(x => x.Solos.Count):F2}");
        var solos = songs.SelectMany(x => x.Solos.Values).ToArray();
        Console.WriteLine($"soloists: {string.Join(", ", solos.SelectMany(x => x.Soloists).GroupBy(x => x).OrderByDescending(x => x.Count()).Select(x => $"{x.Key} {x.Count()}"))}; " +
                          $"several {solos.Count(x => x.Soloists.Length > 1)}, together {solos.Count(x => x.Soloists.Length > 1 && x.Mode == SoloMode.Together)}");
        await Task.CompletedTask;
    }
}

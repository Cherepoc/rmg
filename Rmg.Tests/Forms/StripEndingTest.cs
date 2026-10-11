using System.Collections.Immutable;
using Rmg.Core.Composition;
using Rmg.Core.Probabilities;
using Rmg.Core.Songs;

namespace Rmg.Tests.Forms;

/// <summary>A dance track's ending stripped back (<see cref="EndingKind.Strip" />): its parts leaving one by one, the drums alone at its end.</summary>
public sealed class StripEndingTest
{
    private static Genre Stripping => new(ImmutableDictionary<string, ImmutableDictionary<string, ByConvention>>.Empty
        .Add(GenreChoices.Ending.Name, ImmutableDictionary<string, ByConvention>.Empty.Add(GenreChoices.Name(nameof(EndingKind.Strip)), new ByConvention(1, 1, 1))));

    [Test]
    public async Task AStrippedSong_LosesItsPartsOneByOne_TheDrumsAloneAtItsEnd()
    {
        var songs = 0;
        foreach (var song in TestCorpus.InParallel(Enumerable.Range(0, 32), seed => TestCorpus.Get(seed, new SongOverrides(Genre: Stripping))))
        {
            await Assert.That(song.Map.Ending.Kind).IsEqualTo(EndingKind.Strip);
            var end = song.Map.Ending.Start;
            var bar = song.Map.MeterAt(end - 1e-9).BarDuration;
            var drums = song.Song.Notes!.Where(x => song.Song.TrackDefinitions[x.Key] is PercussionInstrumentTrack).SelectMany(x => x.Value).ToArray();
            if (drums.Length == 0)
                continue;
            songs++;
            // every pitched part's last note sounds out before the last bar, and they leave at bars of their own
            var lasts = song.Song.Notes!.Where(x => song.Song.TrackDefinitions[x.Key] is not PercussionInstrumentTrack && x.Value.Count > 0)
                .GroupBy(x => song.Song.TrackDefinitions[x.Key].Role)
                .Select(x => x.SelectMany(t => t.Value).Max(n => n.Position + n.Value.Duration)).ToArray();
            await Assert.That(lasts.All(x => x <= end - bar + 1e-6)).IsTrue().Because($"seed {song.Seed}");
            await Assert.That(lasts.Select(x => Math.Floor(x / bar + 1e-6)).Distinct().Count()).IsGreaterThan(1).Because($"seed {song.Seed}");
            // and the drums play on to the end
            await Assert.That(drums.Any(x => x.Position >= end - bar - 1e-6)).IsTrue().Because($"seed {song.Seed}");
        }

        await Assert.That(songs).IsGreaterThan(20);
    }
}

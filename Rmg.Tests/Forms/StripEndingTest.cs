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
        var drummed = 0;
        foreach (var song in TestCorpus.InParallel(Enumerable.Range(0, 32), seed => TestCorpus.Get(seed, new SongOverrides(Genre: Stripping))))
        {
            await Assert.That(song.Map.Ending.Kind).IsEqualTo(EndingKind.Strip);
            var end = song.Map.Ending.Start;
            var bar = song.Map.MeterAt(end - 1e-9).BarDuration;
            var drums = song.Song.Notes!.Where(x => song.Song.TrackDefinitions[x.Key] is PercussionInstrumentTrack).SelectMany(x => x.Value).ToArray();
            if (drums.Length == 0)
                continue;
            songs++;
            // every pitched part's last note sounds out before the last bar, and they leave at bars of their own, the drums
            // playing on to the end; where the drums rest there, one part leads out
            var lasts = song.Song.Notes!.Where(x => song.Song.TrackDefinitions[x.Key] is not PercussionInstrumentTrack && x.Value.Count > 0)
                .GroupBy(x => song.Song.TrackDefinitions[x.Key].Role)
                .Select(x => x.SelectMany(t => t.Value).Max(n => n.Position + n.Value.Duration)).ToArray();
            var drumsToTheEnd = drums.Any(x => x.Position >= end - bar - 1e-6);
            await Assert.That(lasts.Count(x => x > end - bar + 1e-6)).IsLessThanOrEqualTo(drumsToTheEnd ? 0 : 1).Because($"seed {song.Seed}");
            await Assert.That(lasts.Select(x => Math.Floor(x / bar + 1e-6)).Distinct().Count()).IsGreaterThan(1).Because($"seed {song.Seed}");
            if (drumsToTheEnd)
                drummed++;
        }

        await Assert.That(songs).IsGreaterThan(20);
        // most stripped songs end on the drums alone
        await Assert.That(drummed).IsGreaterThan(songs * 3 / 4);
    }
}

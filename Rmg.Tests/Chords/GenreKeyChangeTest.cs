using System.Collections.Immutable;
using Rmg.Core.Composition;
using Rmg.Core.Songs;
using Rmg.Tests.Genres;

namespace Rmg.Tests.Chords;

/// <summary>Key changes by genre: a blues in one key however wild, a jazz tune's sections in keys of their own oftener than the songs'.</summary>
public sealed class GenreKeyChangeTest
{
    private static double Changing(Genre genre, double scale) => TestCorpus.InParallel(Enumerable.Range(0, 64), seed => TestCorpus.Get(seed, new SongOverrides(
            Facets: ImmutableDictionary<Facet, double>.Empty.Add(Facet.Scale, scale), Genre: genre)))
        .Average(x => ((ImmutableArray<KeyChange>)x.Trace.Single(t => t.Point == TracePoints.KeyChange).Value!).IsEmpty ? 0.0 : 1);

    private static Genre Without(string id, string choice) => GenrePresets.Without(id, choice);

    [Test]
    public async Task ABlues_StaysInItsKey_HoweverWild_AndAJazzTune_ChangesKeyOftener()
    {
        await Assert.That(Changing(GenrePresets.Genre("blues"), 1)).IsEqualTo(0);
        // the same songs with Jazz's key changes and with the songs' own
        var (jazz, own) = (Changing(GenrePresets.Genre("jazz"), 0.5), Changing(Without("jazz", GenreChoices.KeyChanges.Name), 0.5));
        Console.WriteLine($"songs changing key: Jazz {jazz:P0}, with the songs' own key changes {own:P0}");
        // seen 53% to 62% against 22% to 36%, over three corpora's 64 songs
        await Assert.That(jazz).IsGreaterThan(own);
    }
}

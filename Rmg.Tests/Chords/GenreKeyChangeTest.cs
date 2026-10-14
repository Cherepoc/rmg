using System.Collections.Immutable;
using Rmg.Core.Composition;
using Rmg.Core.Songs;
using Rmg.Tests.Genres;

namespace Rmg.Tests.Chords;

/// <summary>Key changes by genre: a blues in one key however wild, a jazz tune's sections in keys of their own oftener than the songs'.</summary>
public sealed class GenreKeyChangeTest
{
    private static double Changing(Genre genre, double scale) => TestCorpus.InParallel(Enumerable.Range(0, 64), seed => TestCorpus.Get(seed, new SongOverrides(
            Facets: TestCorpus.FacetsOf((Facet.Scale, scale)), Genre: genre)))
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

    /// <summary>
    ///     Symphonic's last section lifted by a third now and then, its own pool's (`keyLift`), where the songs' lifts are a
    ///     half or a whole step at the middle: each song lifting its last section, its sections in the song's key.
    /// </summary>
    [Test]
    public async Task ASymphonicLift_RisesByAThirdNowAndThen_TheSongsNever()
    {
        int[] Lifts(Genre genre) => [..TestCorpus.InParallel(Enumerable.Range(0, 64), seed => TestCorpus.Get(seed, new SongOverrides(Facets: TestCorpus.FacetsOf((Facet.Scale, 0.5)), Genre: genre)))
            .SelectMany(x => (ImmutableArray<KeyChange>)x.Trace.Single(t => t.Point == TracePoints.KeyChange).Value!)
            .Select(x => ((x.Semitones % 12) + 12) % 12)];
        Genre Lifting(Genre genre) => genre with { Choices = genre.Choices.SetItem(GenreChoices.KeyChanges.Name, GenrePresets.With("symphonic", GenreChoices.KeyChanges, ("lastSection", 1), ("sections", 0), ("patterns", 0)).Choices[GenreChoices.KeyChanges.Name]) };
        var symphonic = Lifts(Lifting(GenrePresets.Genre("symphonic")));
        var own = Lifts(Lifting(Without("symphonic", GenreChoices.KeyLift.Name)));
        bool Third(int step) => step is 3 or 4 or 8 or 9;
        Console.WriteLine($"lifts by a third: Symphonic's {symphonic.Count(Third)} of {symphonic.Length}, with the songs' own steps {own.Count(Third)} of {own.Length}");
        await Assert.That(symphonic.Count(Third)).IsGreaterThan(symphonic.Length / 10);
        await Assert.That(own.Count(Third)).IsEqualTo(0);
    }
}

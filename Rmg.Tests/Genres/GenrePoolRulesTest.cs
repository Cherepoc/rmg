using System.Collections.Immutable;
using Rmg.Core;
using Rmg.Core.Composition;
using Rmg.Core.Probabilities;
using Rmg.Core.Songs;

namespace Rmg.Tests.Genres;

/// <summary>A genre's pools of the solos' counts and shares and of the part opening a song, kept to however wild the song.</summary>
public sealed class GenrePoolRulesTest
{
    /// <summary>A preset with choices set, each option at the amount given at every end.</summary>
    private static Genre With(string id, params (GenreChoice Choice, (string Option, double Amount)[] Options)[] choices)
    {
        var genre = GenrePresets.Genre(id);
        return genre with
        {
            Choices = choices.Aggregate(genre.Choices, (all, x) => all.SetItem(x.Choice.Name, x.Options.ToImmutableDictionary(o => o.Option, o => new ByConvention(o.Amount, o.Amount, o.Amount))))
        };
    }

    /// <summary>The songs of a genre at the middle and at the ends of the facet its choice leans by, the melody's for the solos'.</summary>
    private static CorpusSong[] Songs(Genre genre, Facet facet) =>
        TestCorpus.InParallel(Enumerable.Range(0, 48), seed => TestCorpus.Get(seed, new SongOverrides(Genre: genre, Facets: seed % 3 == 0 ? null : TestCorpus.FacetsOf((facet, seed % 3 == 1 ? 0 : 1)))));

    /// <summary>A genre naming duels alone never has three soloists at once, and has two where it can.</summary>
    [Test]
    public async Task AGenresSoloCount_KeepsToItsPool()
    {
        var genre = With("jazz", (GenreChoices.SoloCount, [(Solos.CountName(2), 1)]), (GenreChoices.Solos, [("sections", 0.5), ("trades", 0)]));
        var duels = 0;
        foreach (var song in Songs(genre, Facet.Melody))
        foreach (var (index, solo) in song.Solos.Where(x => !x.Value.IsDrumSolo))
        {
            await Assert.That(solo.Soloists.Length).IsLessThanOrEqualTo(2).Because($"seed {song.Seed}, solo {index}");
            duels += solo.Soloists.Length == 2 ? 1 : 0;
        }

        await Assert.That(duels).IsGreaterThan(0);
    }

    /// <summary>A genre naming a shared solo played together alone never trades the phrases of one its pitched parts share.</summary>
    [Test]
    public async Task AGenresSoloShare_KeepsToItsPool()
    {
        var genre = With("jazz", (GenreChoices.SoloShare, [(GenreChoices.Name(nameof(SoloMode.Together)), 1)]), (GenreChoices.Solos, [("sections", 0.5), ("trades", 0)]));
        var shared = 0;
        foreach (var song in Songs(genre, Facet.Melody))
        foreach (var (index, solo) in song.Solos.Where(x => !x.Value.IsDrumSolo && x.Value.Soloists.Length > 1))
        {
            await Assert.That(solo.Mode).IsEqualTo(SoloMode.Together).Because($"seed {song.Seed}, solo {index}");
            shared++;
        }

        await Assert.That(shared).IsGreaterThan(0);
    }

    /// <summary>A genre naming the drums alone to open its songs opens on them or on no part alone.</summary>
    [Test]
    public async Task AGenresOpeningPart_KeepsToItsPool()
    {
        var genre = With("rock", (GenreChoices.OpeningAlone, [("songs", 1)]), (GenreChoices.OpeningPart, [(GenreChoices.Name(nameof(TrackRole.Drum)), 1)]));
        var drums = 0;
        // at the ends of the form facet, which the opening part leans by
        foreach (var song in Songs(genre, Facet.Form))
        {
            var opening = (TrackRole?)song.Trace.Single(x => x.Point == TracePoints.OpeningAlone).Value;
            await Assert.That(opening is null or TrackRole.Drum).IsTrue().Because($"seed {song.Seed}, {opening}");
            drums += opening == TrackRole.Drum ? 1 : 0;
        }

        await Assert.That(drums).IsGreaterThan(0);
    }
}

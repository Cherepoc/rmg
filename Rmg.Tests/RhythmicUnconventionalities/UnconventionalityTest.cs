using System.Collections.Immutable;
using Rmg.Core.Composition;
using Rmg.Core.Probabilities;

namespace Rmg.Tests.RhythmicUnconventionalities;

public sealed class UnconventionalityTest
{
    private static IGenerationContext Stream(Facet facet) => new GenerationContext((int)facet + 1);

    [Test]
    [Arguments(0.0)]
    [Arguments(1.0)]
    public async Task ABaseAtAnEnd_MakesEveryFacetSo_AndEverySectionToo(double @base)
    {
        var song = Unconventionality.Generate(@base, Stream);
        var section = song.GenerateSection(Stream);

        await Assert.That(song.Facets.Values.All(x => x == @base)).IsTrue();
        await Assert.That(section.Facets.Values.All(x => x == @base)).IsTrue();
        await Assert.That(song.Facets.Count).IsEqualTo(Enum.GetValues<Facet>().Length);
    }

    [Test]
    public async Task ASection_MovesItsFacets_ByUpToItsShift()
    {
        var song = Unconventionality.Generate(0.5, Stream);
        var sections = Enumerable.Range(0, 64).Select(x => song.GenerateSection(facet => new GenerationContext(x * 16 + (int)facet))).ToArray();

        await Assert.That(sections.All(x => x.Facets.All(f => Math.Abs(f.Value - song[f.Key]) <= Unconventionality.SectionShift + 1e-9))).IsTrue();
        await Assert.That(sections.SelectMany(x => x.Facets.Values).Distinct().Count()).IsGreaterThan(1);
    }

    [Test]
    public async Task SongsAtTheEnds_AndWithAFacetAloneWild_StillGenerate()
    {
        SongOverrides[] overrides =
        [
            new(Base: 0),
            new(Base: 1),
            ..Enum.GetValues<Facet>().Select(facet => new SongOverrides(Base: 0, Facets: ImmutableDictionary<Facet, double>.Empty.Add(facet, 1)))
        ];
        var songs = TestCorpus.InParallel(Enumerable.Range(0, overrides.Length * 2), x => TestCorpus.Get(x / 2, overrides[x % overrides.Length]));

        foreach (var song in songs)
        {
            await Assert.That(song.Song.Notes!.Values.Sum(x => x.Count)).IsGreaterThan(0).Because($"seed {song.Seed}");
            await Assert.That(song.Rendered.Tracks.Length).IsGreaterThan(0);
        }
    }
}

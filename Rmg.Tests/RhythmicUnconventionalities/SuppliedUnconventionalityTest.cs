using Rmg.Core;
using Rmg.Core.Composition;
using Rmg.Core.Rendering;
using Rmg.Core.Songs;

namespace Rmg.Tests.RhythmicUnconventionalities;

public sealed class SuppliedUnconventionalityTest
{
    private static byte[] Write(Song song)
    {
        using var stream = new MemoryStream();
        Render.RenderSong(song).Write(stream, null);
        return stream.ToArray();
    }

    [Test]
    [Arguments(0.0)]
    [Arguments(0.3)]
    [Arguments(1.0)]
    public async Task ASuppliedUnconventionality_IsTheSongs(double unconventionality)
    {
        await Assert.That(SongGenerator.GenerateSong(TestCorpus.SongSeed(7), new SongOverrides(Base: unconventionality)).Draws!.Unconventionality.Base).IsEqualTo(unconventionality);
    }

    /// <summary>
    ///     A base, or the facets, supplied as the song drew them leave the song as its seed made it, its other draws from
    ///     streams of their own; and a facet supplied is the song's own.
    /// </summary>
    [Test]
    public async Task AnUnconventionalityGivenAsDrawn_LeavesTheSongAsItsSeedMadeIt_AFacetGivenTheSongs()
    {
        foreach (var seed in Enumerable.Range(0, 8).Select(TestCorpus.SongSeed))
        {
            var drawn = SongGenerator.GenerateSong(seed, SongOverrides.None);
            var own = drawn.Draws!.Unconventionality;
            await Assert.That(Write(SongGenerator.GenerateSong(seed, new SongOverrides(Base: own.Base)))).IsEquivalentTo(Write(drawn), CollectionOrdering.Matching).Because($"seed {seed}, its base");
            await Assert.That(Write(SongGenerator.GenerateSong(seed, new SongOverrides(Facets: own.Facets)))).IsEquivalentTo(Write(drawn), CollectionOrdering.Matching).Because($"seed {seed}, its facets");
            var given = SongGenerator.GenerateSong(seed, new SongOverrides(Facets: TestCorpus.FacetsOf((Facet.Chords, 0.9))));
            await Assert.That(given.Draws!.Unconventionality[Facet.Chords]).IsEqualTo(0.9).Because($"seed {seed}");
        }
    }

    [Test]
    public async Task NoneSupplied_IsTheSongItsSeedMakes()
    {
        var drawn = SongGenerator.GenerateSong(7, SongOverrides.None);

        await Assert.That(Write(drawn)).IsEquivalentTo(Write(SongGenerator.GenerateSong(7)), CollectionOrdering.Matching);
        await Assert.That(drawn.Draws).IsNotNull();
    }

    [Test]
    [Arguments(-0.1)]
    [Arguments(1.1)]
    [Arguments(double.NaN)]
    public async Task AnUnconventionality_OutsideZeroToOne_Fails(double unconventionality)
    {
        await Assert.That(() => SongGenerator.GenerateSong(7, new SongOverrides(Base: unconventionality))).Throws<ArgumentOutOfRangeException>();
    }
}

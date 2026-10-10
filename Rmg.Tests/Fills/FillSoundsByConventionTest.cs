using System.Collections.Immutable;
using Rmg.Core.Composition;
using Rmg.Core.Songs;

namespace Rmg.Tests.Fills;

/// <summary>The fills' sounds by convention: a plain section's fills and landings crash, a wild one's reach for the china and the splash.</summary>
public sealed class FillSoundsByConventionTest
{
    /// <summary>The share of the cymbal's hits of the songs' drums that are the china's or the splash's, at a fills facet.</summary>
    private static double ChinaAndSplash(double fills)
    {
        var hits = TestCorpus.InParallel(Enumerable.Range(0, 64), seed => TestCorpus.Get(seed, new SongOverrides(Facets: ImmutableDictionary<Facet, double>.Empty.Add(Facet.Fills, fills))))
            .SelectMany(x => x.Rendered.Tracks.Where(t => t.IsPercussionInstrument).SelectMany(t => t.NoteTimeline))
            .Select(x => x.Value.Offset).Where(x => x is 49 or 52 or 55 or 57).ToArray();
        Console.WriteLine($"fills {fills}: {hits.Length} cymbal hits, china or splash {hits.Count(x => x is 52 or 55) / (double)hits.Length:P1}");
        return hits.Count(x => x is 52 or 55) / (double)hits.Length;
    }

    [Test]
    public async Task APlainSectionsCymbals_AreCrashes_AndAWildOnesChinasAndSplashes_Oftener()
    {
        // seen 1.4% to 5.2% over three corpora's 64 songs, the groove's cymbal strokes, which the fills facet does not lean,
        // among them
        await Assert.That(ChinaAndSplash(0)).IsLessThan(0.1);
        await Assert.That(ChinaAndSplash(1)).IsGreaterThan(ChinaAndSplash(0.5));
    }
}

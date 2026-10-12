using System.Collections.Immutable;
using Rmg.Core.Composition;
using Rmg.Core.Songs;

namespace Rmg.Tests.Chords;

/// <summary>A chord's own scale: a mode on its root that keeps its triad, a wild song's alone.</summary>
public sealed class ChordOwnScaleTest
{
    private static ChordScale[] Scales(double progression) => TestCorpus.InParallel(Enumerable.Range(0, 32), seed => TestCorpus.Get(seed,
            new SongOverrides(Facets: TestCorpus.FacetsOf((Facet.Progression, progression)))))
        .SelectMany(x => x.Trace.Where(t => t.Point == TracePoints.ChordScales).SelectMany(t => (ImmutableArray<ChordScale>)t.Value!)).ToArray();

    [Test]
    public async Task AWildSongsChords_TakeScalesOfTheirOwn_NowAndThen_AMiddlingOnesNever()
    {
        var wild = Scales(1);
        Console.WriteLine($"of a wild song's chords, {wild.Count(x => x.Kind == ChordScaleKind.Own) / (double)wild.Length:P1} in a scale of their own");
        await Assert.That(wild.Count(x => x.Kind == ChordScaleKind.Own)).IsGreaterThan(10);
        await Assert.That(Scales(0.5).Any(x => x.Kind == ChordScaleKind.Own)).IsFalse();
    }
}

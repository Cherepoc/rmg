using System.Collections.Immutable;
using Rmg.Core.Composition;
using Rmg.Core.Songs;

namespace Rmg.Tests.Chords;

/// <summary>A stretched voicing: a chord spread over two octaves and more, a wilder song's, never a plain one's.</summary>
public sealed class SpreadVoicingTest
{
    private static double TwoOctaves(double chords) => TestCorpus.InParallel(Enumerable.Range(0, 16), seed => TestCorpus.Get(seed,
            new SongOverrides(Facets: ImmutableDictionary<Facet, double>.Empty.Add(Facet.Chords, chords))))
        .SelectMany(x => x.Song.Notes![SongTracks.ChordsTrack]).Where(x => x.Value.Pitches.Length >= 3)
        .Average(x => x.Value.Pitches.Max() - x.Value.Pitches.Min() >= 24 ? 1.0 : 0);

    [Test]
    public async Task AWilderSongsChords_SpreadOverTwoOctaves_NowAndThen_APlainOnesNever()
    {
        await Assert.That(TwoOctaves(0)).IsEqualTo(0);
        await Assert.That(TwoOctaves(0.75)).IsGreaterThan(0.03);
    }
}

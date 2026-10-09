using System.Collections.Immutable;
using Rmg.Core.Composition;
using Rmg.Core.Songs;

namespace Rmg.Tests.Chords;

/// <summary>A stretched voicing: a chord spread over two octaves and more, a wilder song's, never a plain one's.</summary>
public sealed class SpreadVoicingTest
{
    /// <summary>
    ///     The share of the chords' chords of three notes or more spread over two octaves, and the songs that spread any, at
    ///     the chords facet given; a song spreads them only where its chords' instrument and register leave room for it.
    /// </summary>
    private static (double Share, int Songs) TwoOctaves(double chords)
    {
        var songs = TestCorpus.InParallel(Enumerable.Range(0, 64), seed => TestCorpus.Get(seed, new SongOverrides(Facets: ImmutableDictionary<Facet, double>.Empty.Add(Facet.Chords, chords))))
            .Select(x => x.Song.Notes![x.Song.Track(TrackRole.Chords)].Where(n => n.Value.Pitches.Length >= 3).Select(n => n.Value.Pitches.Max() - n.Value.Pitches.Min() >= 24).ToArray())
            .ToArray();
        return (songs.SelectMany(x => x).Average(x => x ? 1.0 : 0), songs.Count(x => x.Any(y => y)));
    }

    [Test]
    public async Task AWilderSongsChords_SpreadOverTwoOctaves_NowAndThen_APlainOnesNever()
    {
        var (plain, wild) = (TwoOctaves(0), TwoOctaves(0.75));
        // (0.1% to 3% of the chords, in 2 songs of 64, over these songs)
        Console.WriteLine($"spread over two octaves: {wild.Share:P1} of the chords in {wild.Songs} songs of 64 at 0.75, {plain.Share:P1} at 0");
        await Assert.That(plain.Share).IsEqualTo(0);
        await Assert.That(wild.Songs).IsGreaterThan(0);
    }
}

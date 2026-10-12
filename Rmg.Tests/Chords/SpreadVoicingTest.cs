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
        var songs = TestCorpus.InParallel(Enumerable.Range(0, 64), seed => TestCorpus.Get(seed, new SongOverrides(Facets: TestCorpus.FacetsOf((Facet.Chords, chords)))))
            .Select(x => x.Song.Notes![x.Song.Track(TrackRole.Chords)].Where(n => n.Value.Pitches.Length >= 3).Select(n => n.Value.Pitches.Max() - n.Value.Pitches.Min() >= 24).ToArray())
            .ToArray();
        return (songs.SelectMany(x => x).Average(x => x ? 1.0 : 0), songs.Count(x => x.Any(y => y)));
    }

    [Test]
    public async Task AWilderSongsChords_SpreadOverTwoOctaves_NowAndThen_APlainOnesNever()
    {
        // the songs: a plain one's chords never spread, wherever its instrument would leave room
        var (plain, wild) = (TwoOctaves(0), TwoOctaves(1));
        // (none at 0; at 1, 0% to 2.6% of the chords, in no song to one of 64, over three corpora's, the chords' instrument
        // and register pulling most spreads back in)
        Console.WriteLine($"spread over two octaves: {wild.Share:P1} of the chords in {wild.Songs} songs of 64 at 1, {plain.Share:P1} at 0");
        await Assert.That(plain.Share).IsEqualTo(0);

        // the voicing drawn: a triad laid out over two octaves now and then at the wild end, never at the plain
        var triad = ChordShapes.All.Single(x => x.Name == "Triad");
        double Spread(double chords)
        {
            var context = new Rmg.Core.Probabilities.GenerationContext(1);
            return Enumerable.Range(0, 2_000).Average(_ => ChordVoicing.Apply(context, triad, chords) is var x && x.Max() - x.Min() >= 24 ? 1.0 : 0);
        }

        // its weight 0.5 of 4.5 at 1, about 11%, and none at 0
        await Assert.That(Spread(0)).IsEqualTo(0);
        await Assert.That(Spread(1)).IsGreaterThan(0.05);
    }
}

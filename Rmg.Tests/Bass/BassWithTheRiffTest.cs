using System.Collections.Immutable;
using Rmg.Core.Composition;
using Rmg.Core.Songs;

namespace Rmg.Tests.Bass;

/// <summary>The bass with the riff (<see cref="LineDoubling.RiffChance" />): a convention, a plain band's always, a wild one's seldom.</summary>
public sealed class BassWithTheRiffTest
{
    /// <summary>The share of a song's sections with a riff whose bass doubles it.</summary>
    private static (int Doubled, int Riffs) Measure(CorpusSong song)
    {
        var riff = song.Song.Notes!.GetValueOrDefault(song.Track(TrackRole.Riff));
        var doubled = song.Trace.Where(t => t.Point == TracePoints.LineDoubling && t.Value is (int, TrackRole.Riff, TrackRole.Bass, int)).Select(t => (((int Index, TrackRole, TrackRole, int))t.Value!).Index).ToHashSet();
        var riffs = song.Map.Sections.Select((x, i) => (x, i)).Where(x => riff?.Any(n => x.x.IsWithin(n.Position)) == true).Select(x => x.i).ToArray();
        return (riffs.Count(doubled.Contains), riffs.Length);
    }

    [Test]
    public async Task TheBassDoublesTheRiff_ThePlainerTheOftener()
    {
        double Share(double melody)
        {
            var overrides = new SongOverrides(Facets: TestCorpus.FacetsOf((Facet.Melody, melody)), Parts: ImmutableDictionary<TrackRole, bool>.Empty.Add(TrackRole.Riff, true));
            var measured = TestCorpus.InParallel(Enumerable.Range(0, 64), seed => Measure(TestCorpus.Get(seed, overrides))).ToArray();
            return measured.Sum(x => x.Doubled) / (double)measured.Sum(x => x.Riffs);
        }

        var shares = new[] { 0.0, 0.25, 0.5, 0.75, 1.0 }.Select(x => (Melody: x, Share: Share(x))).ToArray();
        // against the mechanism breaking (measured: 96% to 97% at 0, 46% to 56% between, none at 1, at offsets 0 and a million)
        Console.WriteLine($"sections with a riff the bass doubles: {string.Join(", ", shares.Select(x => $"{x.Share:P0} at {x.Melody}"))}");
        await Assert.That(shares[0].Share).IsGreaterThan(0.8);
        await Assert.That(shares[^1].Share).IsLessThan(0.1);
        await Assert.That(shares.Zip(shares.Skip(1)).All(x => x.First.Share >= x.Second.Share - 0.05)).IsTrue();
    }

    /// <summary>Where the twin leads, the riff a lead line, the bass plays its own beneath it, never the lead in unison.</summary>
    [Test]
    public async Task WhereTheTwinLeads_TheBassPlaysItsOwn()
    {
        var songs = TestCorpus.InParallel(Enumerable.Range(0, 64), seed => TestCorpus.Get(seed, new SongOverrides(Genre: Genres.GenrePresets.Genre("metal"))));
        HashSet<int> Doubled(CorpusSong song, TrackRole target) =>
            [..song.Trace.Where(t => t.Point == TracePoints.LineDoubling && t.Value is (int, TrackRole.Riff, TrackRole, int) && ((ValueTuple<int, TrackRole, TrackRole, int>)t.Value!).Item3 == target)
                .Select(t => ((ValueTuple<int, TrackRole, TrackRole, int>)t.Value!).Item1)];
        var (twinLed, both) = (songs.Sum(x => Doubled(x, TrackRole.RiffTwin).Count), songs.Sum(x => Doubled(x, TrackRole.RiffTwin).Intersect(Doubled(x, TrackRole.Bass)).Count()));
        Console.WriteLine($"Metal's sections the twin leads: {twinLed}, {both} of them with the bass doubling the riff");
        await Assert.That(twinLed).IsGreaterThan(10);
        await Assert.That(both).IsEqualTo(0);
    }
}

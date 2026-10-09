using System.Collections.Immutable;
using Rmg.Core;
using Rmg.Core.Composition;
using Rmg.Core.Songs;

namespace Rmg.Tests.Arrangements;

public sealed class SongPartsTest
{
    private static ImmutableHashSet<TrackRole> Absent(CorpusSong song) =>
        (ImmutableHashSet<TrackRole>)song.Trace.Single(x => x.Point == TracePoints.SongParts).Value!;

    private static SongOverrides Given(TrackRole part, bool isIn) => new(Parts: ImmutableDictionary<TrackRole, bool>.Empty.Add(part, isIn));

    // the notes of a part's tracks, the drums' all together
    private static int Notes(CorpusSong song, TrackRole part) =>
        song.Song.Notes!.Where(x => song.Song.TrackDefinitions[x.Key].Role == part).Sum(x => x.Value.Count);

    private static byte[] Write(CorpusSong song)
    {
        using var stream = new MemoryStream();
        song.Rendered.Write(stream, null);
        return stream.ToArray();
    }

    [Test]
    public async Task ASong_LeavesOutThePad_TheCounterMelody_AndTheDrums_NowAndThen_AndNeverTheRest()
    {
        var absent = TestCorpus.Measure(256, Absent);
        double Share(TrackRole part) => absent.Count(x => x.Contains(part)) / (double)absent.Length;

        // bounded widely, the targets in the Report
        Console.WriteLine($"left out: the pad {Share(TrackRole.Pad):P0}, the counter-melody {Share(TrackRole.CounterMelody):P0}, the drums {Share(TrackRole.Drum):P0}");
        await Assert.That(Share(TrackRole.Pad)).IsBetween(0.05, 0.4);
        await Assert.That(Share(TrackRole.CounterMelody)).IsBetween(0.2, 0.6);
        await Assert.That(Share(TrackRole.Drum)).IsBetween(0.005, 0.15);
        await Assert.That(absent.Any(x => x.Overlaps([TrackRole.Melody, TrackRole.Chords, TrackRole.Bass]))).IsFalse();
    }

    /// <summary>How often the songs leave each part out, against its target: the pad a fifth, the counter-melody two fifths, the drums one in twenty.</summary>
    [Test]
    [Explicit]
    public async Task Report()
    {
        var absent = TestCorpus.Measure(1024, Absent);
        foreach (var (part, target) in new[] { (TrackRole.Pad, 0.2), (TrackRole.CounterMelody, 0.4), (TrackRole.Drum, 0.05) })
            Console.WriteLine($"{part}: left out of {absent.Count(x => x.Contains(part)) / (double)absent.Length:P1}, the target {target:P0}");
        await Task.CompletedTask;
    }

    [Test]
    [Arguments(TrackRole.Pad)]
    [Arguments(TrackRole.Drum)]
    [Arguments(TrackRole.Bass)]
    [Arguments(TrackRole.Melody)]
    public async Task APartGivenOut_PlaysNoNote(TrackRole part)
    {
        foreach (var seed in Enumerable.Range(0, 8))
            await Assert.That(Notes(TestCorpus.Get(seed, Given(part, false)), part)).IsEqualTo(0);
    }

    [Test]
    public async Task APartGivenIn_IsInTheSong_AndGivenAsDrawn_LeavesTheSongAsItsSeedMadeIt()
    {
        foreach (var seed in Enumerable.Range(0, 16))
        {
            var drawn = TestCorpus.Get(seed);
            var isIn = !Absent(drawn).Contains(TrackRole.Pad);
            var given = TestCorpus.Get(seed, Given(TrackRole.CounterMelody, true));

            await Assert.That(Absent(given).Contains(TrackRole.CounterMelody)).IsFalse();
            await Assert.That(Write(TestCorpus.Get(seed, Given(TrackRole.Pad, isIn)))).IsEquivalentTo(Write(drawn), CollectionOrdering.Matching);
        }
    }

    [Test]
    public async Task APartDrawnOut_ComesIn_WhereEveryOtherIsGivenOut()
    {
        // all but the riff given out, which draws itself out in about half the songs
        var parts = SongParts.All.Where(x => x is not (TrackRole.Riff or TrackRole.RiffTwin)).ToImmutableDictionary(x => x, _ => false);
        var riffs = 0;
        foreach (var song in TestCorpus.InParallel(Enumerable.Range(0, 16), seed => TestCorpus.Get(seed, new SongOverrides(Parts: parts))))
        {
            // the riff where the seed drew it out, its part playing as it comes in
            var riff = song.Song.TrackDefinitions.Single(x => x.Value.Role == TrackRole.Riff).Key;
            riffs += Absent(TestCorpus.Get(song.Seed)).Contains(TrackRole.Riff) ? 1 : 0;
            await Assert.That(song.Song.Notes![riff].Count).IsGreaterThan(0);
        }

        // the riff drawn out in some of the seeds, the case this is about
        await Assert.That(riffs).IsGreaterThan(0);
        await Assert.That(SongParts.LeaveNone(parts)).IsFalse();
        await Assert.That(SongParts.LeaveNone(parts.Add(TrackRole.Riff, false))).IsTrue();
    }

    [Test]
    public async Task EveryPartGivenOut_IsNoSong()
    {
        var parts = SongParts.All.ToImmutableDictionary(x => x, _ => false);

        await Assert.That(() => SongGenerator.GenerateSong(1, new SongOverrides(Parts: parts))).Throws<ArgumentException>();
    }
}

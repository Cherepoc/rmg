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

        await Assert.That(Share(TrackRole.Pad)).IsEqualTo(0.2).Within(0.06);
        await Assert.That(Share(TrackRole.CounterMelody)).IsEqualTo(0.4).Within(0.07);
        await Assert.That(Share(TrackRole.Drum)).IsEqualTo(0.05).Within(0.04);
        await Assert.That(absent.Any(x => x.Overlaps([TrackRole.Melody, TrackRole.Chords, TrackRole.Bass]))).IsFalse();
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
            await Assert.That(Write(TestCorpus.Get(seed, Given(TrackRole.Pad, isIn)))).IsEquivalentTo(Write(drawn));
        }
    }

    [Test]
    public async Task APartDrawnOut_ComesIn_WhereEveryOtherIsGivenOut()
    {
        // all but the riff given out, which draws itself out in about half the songs
        var parts = SongParts.All.Where(x => x is not (TrackRole.Riff or TrackRole.RiffTwin)).ToImmutableDictionary(x => x, _ => false);
        for (ulong seed = 0; seed < 16; seed++)
            await Assert.That(SongGenerator.GenerateSong(seed, new SongOverrides(Parts: parts)).Notes![SongTracks.RiffTrack].Count).IsGreaterThan(0);
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

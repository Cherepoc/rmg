using Rmg.Core.Composition;
using Rmg.Core.Events;

namespace Rmg.Tests.Chords;

public sealed class ChordRegisterTest
{
    // whether each bar of the chords in the song's sections starts afresh in a register of its own
    private static bool[] Afresh(CorpusSong song) => song.Song.Notes![SongTracks.ChordsTrack]
        .Where(x => song.Map.SectionAt(x.Position) is not null)
        .GroupBy(x => (int)Math.Floor(x.Position / song.Map.Meter.BarDuration + 1e-9))
        .Select(x => x.First().Value.State.GetStateValue(StateKinds.ChordVoicingReset) > 0)
        .ToArray();

    [Test]
    public async Task ThePlainestChords_AreAllLedFromTheOneBefore_AndTheWildestStartEveryBarAfresh()
    {
        foreach (var seed in Enumerable.Range(0, 4))
        {
            await Assert.That(Afresh(TestCorpus.Get(seed, new SongOverrides(Base: 0))).Any(x => x)).IsFalse();
            await Assert.That(Afresh(TestCorpus.Get(seed, new SongOverrides(Base: 1))).All(x => x)).IsTrue();
        }
    }
}

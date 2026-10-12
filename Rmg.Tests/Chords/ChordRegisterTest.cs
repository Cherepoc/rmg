using Rmg.Core.Composition;
using Rmg.Core.Events;

namespace Rmg.Tests.Chords;

public sealed class ChordRegisterTest
{
    // whether each bar of the chords in the song's sections starts afresh in a register of its own
    private static bool[] Afresh(CorpusSong song) => song.Song.Notes![song.Song.Track(TrackRole.Chords)]
        .Where(x => song.Map.SectionAt(x.Position) is not null)
        // by the section's bars, each in its own meter
        .GroupBy(x => (song.Map.SectionAt(x.Position)!.Start, song.Map.PlaceOf(x.Position).Bar))
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

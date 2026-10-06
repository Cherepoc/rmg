using Rmg.Core.Composition;
using Rmg.Core.Events;
using Rmg.Core.Songs;

namespace Rmg.Tests.Grooves;

public sealed class PocketTest
{
    /// <summary>A part's offset off the beat in seconds, its first note's, at the tempo where it plays.</summary>
    private static double? Seconds(CorpusSong song, int track) =>
        song.Song.Notes!.GetValueOrDefault(track)?.FirstOrDefault(x => x.Value.State.GetStateValue(CompositionStateKinds.TimingOffset) != 0) is { Value: not null } note
            ? note.Value.State.GetStateValue(CompositionStateKinds.TimingOffset) * 60
              / (song.Song.TrackEventStateTimelineMap.CommonStateTimelineMap.GetEffectiveStateMapAt(note.Position).GetStateValue(StateKinds.Tempo) * Meter.BaseTempo)
            : null;

    [Test]
    public async Task APartsPocket_IsAsLongInSeconds_AtAnyTempo()
    {
        var compared = 0;
        foreach (var seed in Enumerable.Range(0, 16))
        {
            var (slow, fast) = (TestCorpus.Get(seed, new SongOverrides(Tempo: 0.6)), TestCorpus.Get(seed, new SongOverrides(Tempo: 1.4)));
            foreach (var track in new[] { SongTracks.MelodyTrack, SongTracks.BassTrack })
                if (Seconds(slow, track) is { } a && Seconds(fast, track) is { } b)
                {
                    await Assert.That(a).IsEqualTo(b).Within(1e-9).Because($"seed {seed}, track {track}");
                    compared++;
                }
        }

        await Assert.That(compared).IsGreaterThan(0);
    }
}

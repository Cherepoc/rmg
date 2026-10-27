using Rmg.Core.Composition;
using Rmg.Core.Events;
using Rmg.Core.Songs;
using Rmg.Tests.Genres;

namespace Rmg.Tests.Grooves;

/// <summary>A pinned drum: every bar of every section playing the rhythm its song gives it, a four on the floor's kick.</summary>
public sealed class PinnedDrumTest
{
    [Test]
    public async Task APinnedDrum_PlaysItsSongsRhythmSettings_InEveryBar()
    {
        var pinnedBars = 0;
        foreach (var song in TestCorpus.Streamed(Enumerable.Range(0, 16), seed => TestCorpus.Get(seed, new SongOverrides(Genre: GenrePresets.Genre("electronic")))))
        foreach (var (track, definition) in song.Song.TrackDefinitions.Where(x => x.Value.StateMap.GetStateValue(CompositionStateKinds.Pinned) > 0))
        {
            var given = definition.StateMap.Subset(DrumRoles.PinnedKinds);
            foreach (var bar in song.Trace.Where(x => x.Point == TracePoints.BarPattern && x.Track == track))
            {
                pinnedBars++;
                foreach (var kind in DrumRoles.PinnedKinds)
                    await Assert.That(bar.StateMap.Subset([kind]).Equals(given.Subset([kind]))).IsTrue().Because($"{kind}");
            }
        }

        await Assert.That(pinnedBars).IsGreaterThan(100);
    }
}

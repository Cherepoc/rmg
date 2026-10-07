using Rmg.Core;
using Rmg.Core.Composition;
using Rmg.Core.Rendering;
using Rmg.Core.Songs;
using Rmg.Tests.Genres;

namespace Rmg.Tests.Rendering;

/// <summary>An echo as the synthesizer's delay: the echoing part's send to it, the delay's time the echo's at the tempo.</summary>
public sealed class DelayTest
{
    [Test]
    public async Task AnEchoingSong_SendsItsEchoingPartsToTheDelay()
    {
        var echoing = 0;
        foreach (var song in TestCorpus.InParallel(Enumerable.Range(0, 64), seed => TestCorpus.Get(seed, new SongOverrides(Genre: GenrePresets.Genre("reggae")))))
        {
            var echoes = song.Song.Notes!.Values.Any(x => x.Any(n => n.Value.State.GetStateValue(CompositionStateKinds.Echo) > 0));
            await Assert.That(song.Rendered.Echo is not null).IsEqualTo(echoes).Because($"seed {song.Seed}");
            if (!echoes)
            {
                await Assert.That(song.Rendered.Tracks.Any(x => x.Controllers.Any(c => c.Controller == ExpressionRender.DelayController && c.Value > 0))).IsFalse();
                continue;
            }

            echoing++;
            // a part sent to the delay, which plays its echo
            await Assert.That(song.Rendered.Tracks.Any(x => !x.IsPercussionInstrument && x.Controllers.Any(c => c.Controller == ExpressionRender.DelayController && c.Value > 0))).IsTrue();

            // a dub's snare by its own send, the channel's too
            var drums = song.Rendered.Tracks.SingleOrDefault(x => x.IsPercussionInstrument);
            var snareEchoes = DrumGroups.Snare.Drums.Select(DrumGroups.GetTrackNumber).Any(x => song.Song.Notes!.GetValueOrDefault(x)?.Any(n => n.Value.State.GetStateValue(CompositionStateKinds.Echo) > 0) == true);
            if (snareEchoes && drums is not null)
            {
                await Assert.That(drums.Controllers.Any(c => c.Controller == ExpressionRender.DelayController && c.Value > 0)).IsTrue();
                await Assert.That(drums.Controllers.Any(c => c.Controller == 99 && c.Value == 31)).IsTrue();
            }
        }

        await Assert.That(echoing).IsGreaterThan(5);
    }

    [Test]
    [Arguments(0.5, 5)]
    [Arguments(2.0, 20)]
    [Arguments(375.0, 99)]
    [Arguments(500.0, 105)]
    [Arguments(1000.0, 115)]
    [Arguments(1500.0, 110)]
    public async Task TheDelaysTime_IsItsDataStep_ASecondAtMost_ALongerHalved(double milliseconds, int step) =>
        await Assert.That((int)Midi.DelayTime(milliseconds)).IsEqualTo(step);
}

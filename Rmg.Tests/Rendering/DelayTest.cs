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
                // every echoing snare's note sent on its own, its parameter named and set in full
                var sent = drums.Controllers.Select(c => (c.Controller, c.Value)).ToArray();
                var keys = DrumGroups.Snare.Drums.Select(DrumGroups.GetTrackNumber)
                    .SelectMany(x => song.Song.Notes!.GetValueOrDefault(x)?.Where(n => n.Value.State.GetStateValue(CompositionStateKinds.Echo) > 0).Select(n => n.Value.Pitches[0]) ?? [])
                    .Distinct().ToArray();
                foreach (var key in keys)
                    await Assert.That(Enumerable.Range(0, sent.Length - 2).Any(i => sent[i] == (Render.NrpnHigh, Render.DrumDelayNrpn) && sent[i + 1] == (Render.NrpnLow, key) && sent[i + 2] == (Render.DataEntry, Render.FullSend)))
                        .IsTrue().Because($"seed {song.Seed}, note {key}");
            }
        }

        await Assert.That(echoing).IsGreaterThan(5);
    }

    /// <summary>A cascade's echoing parts sent to the delay in full, its echoes repeating once, and an ordinary echo's at its send, dying away, an intro's and an ending's notes as their sources'.</summary>
    [Test]
    [Arguments(EchoKind.Cascade, ExpressionRender.CascadeSend, true)]
    [Arguments(EchoKind.DottedEighth, ExpressionRender.DelaySend, false)]
    public async Task AnEcho_IsSentAsItsKindIs(EchoKind kind, int send, bool once)
    {
        var dub = GenrePresets.With("dub", GenreChoices.Echo, (GenreChoices.Name(kind.ToString()), 1));
        var echoing = 0;
        foreach (var song in TestCorpus.InParallel(Enumerable.Range(0, 32), seed => TestCorpus.Get(seed, new SongOverrides(Genre: dub))))
        {
            if (song.Rendered.Echo is null)
                continue;
            echoing++;
            await Assert.That(song.Rendered.EchoOnce).IsEqualTo(once).Because($"seed {song.Seed}");
            var sends = song.Rendered.Tracks.Where(x => !x.IsPercussionInstrument).SelectMany(x => x.Controllers).Where(c => c.Controller == ExpressionRender.DelayController && c.Value > 0).Select(c => c.Value).Distinct().ToArray();
            await Assert.That(sends).IsEquivalentTo(new[] { send }).Because($"seed {song.Seed}");
        }

        await Assert.That(echoing).IsGreaterThan(3);
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

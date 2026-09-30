using Rmg.Core;
using Rmg.Core.Composition;
using Rmg.Core.Events;

namespace Rmg.Tests.SongGenerators;

public sealed class GivenTempoKeyMeterTest
{
    private static StateMap Start(CorpusSong song) => song.Song.TrackEventStateTimelineMap.CommonStateTimelineMap.GetEffectiveStateMapAt(0);

    private static byte[] Write(CorpusSong song)
    {
        using var stream = new MemoryStream();
        song.Rendered.Write(stream, null);
        return stream.ToArray();
    }

    [Test]
    public async Task AGivenTempoKeyAndMeter_AreTheSongs()
    {
        var song = TestCorpus.Get(3, new SongOverrides(Tempo: 0, Key: 7, MeterOption: 2));

        await Assert.That(Start(song).GetStateValue(StateKinds.Tempo)).IsEqualTo(SongGenerator.TempoOptions[0]);
        await Assert.That(Start(song).GetStateValue(StateKinds.KeyOffset)).IsEqualTo(7);
        await Assert.That(Meter.OptionOf(song.Map.Meter)).IsEqualTo(2);
    }

    [Test]
    public async Task GivenAsDrawn_LeavesTheSongAsItsSeedMadeIt()
    {
        foreach (var seed in Enumerable.Range(0, 4))
        {
            var drawn = TestCorpus.Get(seed);
            var given = TestCorpus.Get(seed, new SongOverrides(
                Tempo: SongGenerator.TempoOptions.IndexOf(Start(drawn).GetStateValue(StateKinds.Tempo)),
                Key: Start(drawn).GetStateValue(StateKinds.KeyOffset),
                MeterOption: Meter.OptionOf(drawn.Map.Meter)
            ));

            await Assert.That(Write(given)).IsEquivalentTo(Write(drawn));
        }
    }

    [Test]
    public async Task TheTempos_RunFrom90ToAbout175BeatsAMinute()
    {
        await Assert.That(SongGenerator.TempoOptions[0] * Meter.BaseTempo).IsEqualTo(90);
        await Assert.That(SongGenerator.TempoOptions[^1] * Meter.BaseTempo).IsEqualTo(174.5).Within(0.1);
        await Assert.That(SongGenerator.TempoOptions.Length).IsLessThanOrEqualTo(64);
    }
}

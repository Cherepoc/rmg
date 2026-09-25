using Rmg.Core.Composition;
using Rmg.Core.Rendering;

namespace Rmg.Tests.SongGenerators;

public sealed class SongGeneratorTempoTest
{
    private static double GetTempo(int seed)
    {
        var tempoTimeline = Render.RenderSong(SongGenerator.GenerateSong(seed)).TempoTimeline;
        return tempoTimeline.GetEffectiveValueAt(0);
    }

    [Test]
    public async Task Tempo_IsBetween90And180Bpm_AndVariesBetweenSongs()
    {
        var tempos = Enumerable.Range(0, 30).Select(GetTempo).ToArray();

        await Assert.That(tempos.All(x => x is >= 0.75 and <= 1.5)).IsTrue();
        await Assert.That(tempos.Distinct().Count()).IsGreaterThan(3);
    }
}

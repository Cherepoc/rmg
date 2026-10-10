using Rmg.Core.Composition;

namespace Rmg.Tests.Meters;

/// <summary>The first seeds of every meter, to listen to.</summary>
public sealed class MeterSeedsReportTest
{
    [Test]
    [Explicit]
    public async Task Report()
    {
        // the meter the song draws, its decisions alone made
        static Meter Of(int seed) => SongDecisions.Draw(TestCorpus.SongSeed(seed), SongOverrides.None, CancellationToken.None).Meter;

        foreach (var meter in Enumerable.Range(0, 3000).Select(seed => (Seed: seed, Meter: Of(seed))).GroupBy(x => x.Meter.TimeSignature))
            Console.WriteLine($"{meter.Key.Numerator}/{meter.Key.Denominator}: {string.Join(", ", meter.Take(4).Select(x => $"{x.Seed} ({string.Join("+", x.Meter.Groups)})"))}");
        await Task.CompletedTask;
    }
}

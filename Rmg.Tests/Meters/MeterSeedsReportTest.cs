using Rmg.Core.Composition;

namespace Rmg.Tests.Meters;

/// <summary>The first seeds of every meter, to listen to.</summary>
public sealed class MeterSeedsReportTest
{
    [Test]
    [Explicit]
    public async Task Report()
    {
        // the meter drawn as the song draws it, from its own stream, by the song's feel facet
        static Meter Of(int seed) => Meter.Draw(
            SongGenerator.CreateStream((ulong)seed, SongStream.Meter),
            Unconventionality.Generate(
                Unconventionality.DrawBase(SongGenerator.CreateStream((ulong)seed, SongStream.Rhythm)),
                facet => SongGenerator.CreateStream((ulong)seed, SongStream.Unconventionality, facet)
            )[Facet.Feel],
            null
        );

        foreach (var meter in Enumerable.Range(0, 3000).Select(seed => (Seed: seed, Meter: Of(seed))).GroupBy(x => x.Meter.TimeSignature))
            Console.WriteLine($"{meter.Key.Numerator}/{meter.Key.Denominator}: {string.Join(", ", meter.Take(4).Select(x => $"{x.Seed} ({string.Join("+", x.Meter.Groups)})"))}");
        await Task.CompletedTask;
    }
}

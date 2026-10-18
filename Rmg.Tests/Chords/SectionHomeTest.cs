using Rmg.Core.Composition;

namespace Rmg.Tests.Chords;

/// <summary>
///     A section's home (<see cref="Progressions.GenerateHome" />): the tonic, the relative key, the fourth or the fifth,
///     leaning to the tonic and its relative at the plain end and away from them at the wild, a bridge away the most.
/// </summary>
public sealed class SectionHomeTest
{
    /// <summary>Every section's home of a song, as a step from its tonic, by its section.</summary>
    private static Dictionary<int, int> Homes(CorpusSong song) =>
        song.Trace.Where(x => x.Point == TracePoints.SectionHome).ToDictionary(x => x.Section, x => (int)x.Value!);

    [Test]
    public async Task ThePlainestSongs_NeverHomeOnTheFifth()
    {
        foreach (var seed in Enumerable.Range(0, 16))
            await Assert.That(Homes(TestCorpus.Get(seed, new SongOverrides(Base: 0))).Values.Contains(-3)).IsFalse().Because($"seed {seed}");
    }

    [Test]
    [Explicit]
    public async Task Report()
    {
        var sections = TestCorpus.Measure(256, song =>
        {
            var structure = (SongStructure)song.Trace.Single(x => x.Point == TracePoints.SongForm).Value!;
            var facets = song.Trace.Where(x => x.Point == TracePoints.SectionUnconventionality).ToDictionary(x => x.Section, x => ((Unconventionality)x.Value!)[Facet.Progression]);
            return Homes(song).Select(x => (Band: Math.Clamp((int)(facets[x.Key] * 3), 0, 2), Bridge: structure.Roles[x.Key] == SectionRole.Bridge, Home: x.Value)).ToArray();
        }).SelectMany(x => x).ToArray();

        foreach (var bridge in new[] { false, true })
        foreach (var band in Enumerable.Range(0, 3))
        {
            var homes = sections.Where(x => x.Bridge == bridge && x.Band == band).Select(x => x.Home).ToArray();
            string Pct(Func<int, bool> takes) => $"{100.0 * homes.Count(takes) / Math.Max(1, homes.Length):0}%";
            Console.WriteLine($"{(bridge ? "bridges" : "others")}, progression {band}/3 to {band + 1}/3, {homes.Length} sections: tonic {Pct(x => x == 0)}, " +
                              $"relative {Pct(x => x is 2 or -2)}, fourth {Pct(x => x == 3)}, fifth {Pct(x => x == -3)}");
        }

        await Task.CompletedTask;
    }
}

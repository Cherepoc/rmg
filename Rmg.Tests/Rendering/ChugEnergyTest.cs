using Rmg.Core.Composition;
using Rmg.Core.Songs;
using Rmg.Tests.Genres;

namespace Rmg.Tests.Rendering;

/// <summary>A section's chugs leaning to its quieter sections, as a band mutes its verses and opens its choruses.</summary>
public sealed class ChugEnergyTest
{
    /// <summary>
    ///     Punk's and Metal's sections quieter than their song's middle chug oftener than its louder ones.
    /// </summary>
    [Test]
    [Arguments("punk")]
    [Arguments("metal")]
    public async Task AQuieterSection_ChugsOftener(string preset)
    {
        var (quiet, loud) = (new List<bool>(), new List<bool>());
        foreach (var song in TestCorpus.InParallel(Enumerable.Range(0, 64), seed => TestCorpus.Get(seed, new SongOverrides(Genre: GenrePresets.Genre(preset)))))
        {
            var energies = song.Trace.Where(x => x.Point == TracePoints.SectionEnergy).DistinctBy(x => x.Section).ToDictionary(x => x.Section, x => ((SectionEnergyTrace)x.Value!).Energy);
            var chugs = song.Trace.Where(x => x.Point == TracePoints.Chugs).DistinctBy(x => x.Section).ToDictionary(x => x.Section, x => (bool)x.Value!);
            var middle = energies.Values.Order().ElementAt(energies.Count / 2);
            foreach (var (section, energy) in energies.Where(x => chugs.ContainsKey(x.Key) && Math.Abs(x.Value - middle) > 1e-9))
                (energy < middle ? quiet : loud).Add(chugs[section]);
        }

        var (quietShare, loudShare) = (quiet.Average(x => x ? 1.0 : 0), loud.Average(x => x ? 1.0 : 0));
        Console.WriteLine($"{preset}: chugs in {quietShare:P0} of {quiet.Count} quieter sections, {loudShare:P0} of {loud.Count} louder");
        // by a gap the songs without the lean do not reach (measured over 64: Punk 66% to 70% against 43% to 46%, Metal 59% to
        // 61% against 40% to 47%, at offsets 0 and a million; 5.5 and 6.7 points in Metal at offsets 942000000 and 716000000
        // once; with the lean off, Punk 31% against 22%, Metal 64% against 62%)
        await Assert.That(quietShare - loudShare).IsGreaterThan(preset == "punk" ? 0.12 : 0.04);
    }
}

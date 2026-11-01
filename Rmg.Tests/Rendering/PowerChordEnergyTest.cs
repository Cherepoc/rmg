using Rmg.Core.Composition;
using Rmg.Core.Songs;
using Rmg.Tests.Genres;

namespace Rmg.Tests.Rendering;

/// <summary>A section's riff in power chords leaning to its louder sections, a verse's single notes coming back in power chords in the chorus.</summary>
public sealed class PowerChordEnergyTest
{
    /// <summary>
    ///     Rock's and Nu metal's sections louder than their song's middle play their riff in power chords oftener than its
    ///     quieter ones (measured over 64: Rock's 72% against 55%, Nu metal's 83% against 59%, and without the lean 61% against
    ///     66% and 67% against 72%; over 256, as 64 at another offset gave Nu metal's 71% against 65%, Rock's 73-77% against
    ///     47-54% and Nu metal's 78-81% against 56-60% at three offsets).
    /// </summary>
    [Test]
    [Arguments("rock")]
    [Arguments("nuMetal")]
    public async Task ALouderSection_PlaysItsRiffInPowerChordsOftener(string preset)
    {
        var (quiet, loud) = (new List<bool>(), new List<bool>());
        foreach (var song in TestCorpus.Streamed(Enumerable.Range(0, 256), seed => TestCorpus.Get(seed, new SongOverrides(Genre: GenrePresets.Genre(preset)))))
        {
            var energies = song.Trace.Where(x => x.Point == TracePoints.SectionEnergy).DistinctBy(x => x.Section).ToDictionary(x => x.Section, x => ((SectionEnergyTrace)x.Value!).Energy);
            var powerChords = song.Trace.Where(x => x.Point == TracePoints.RiffPowerChords).DistinctBy(x => x.Section).ToDictionary(x => x.Section, x => (bool)x.Value!);
            var middle = energies.Values.Order().ElementAt(energies.Count / 2);
            foreach (var (section, energy) in energies.Where(x => powerChords.ContainsKey(x.Key) && Math.Abs(x.Value - middle) > 1e-9))
                (energy < middle ? quiet : loud).Add(powerChords[section]);
        }

        var (quietShare, loudShare) = (quiet.Average(x => x ? 1.0 : 0), loud.Average(x => x ? 1.0 : 0));
        Console.WriteLine($"{preset}: power chords in {quietShare:P0} of {quiet.Count} quieter sections, {loudShare:P0} of {loud.Count} louder");
        // by a gap the songs without the lean do not reach, five points the other way
        await Assert.That(loudShare - quietShare).IsGreaterThan(0.1);
    }
}

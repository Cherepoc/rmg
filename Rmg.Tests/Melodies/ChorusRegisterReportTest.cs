using Rmg.Core.Composition;
using Rmg.Core.Songs;

namespace Rmg.Tests.Melodies;

/// <summary>Where the chorus's melody sits against the verse's.</summary>
public sealed class ChorusRegisterReportTest
{
    /// <summary>The median of the melody's pitches over the sections of a role, as the song plays them; none where it plays none.</summary>
    private static double? Median(CorpusSong song, SongStructure structure, SectionRole role)
    {
        var spans = song.Map.Sections.Where(x => structure.Roles[x.SectionId] == role && !song.IsSolo(x)).ToArray();
        int[] pitches =
        [
            ..song.Song.Notes!.GetValueOrDefault(song.Song.Track(TrackRole.Melody))?.Where(x => spans.Any(s => x.Position >= s.Start && x.Position < s.End)).Select(x => x.Value.Pitches.Min()) ?? []
        ];
        return pitches.Length == 0 ? null : pitches.Order().ElementAt(pitches.Length / 2);
    }

    /// <summary>How far above the verse's the chorus's melody sits, in semitones, in a song with both.</summary>
    internal static double? Lift(CorpusSong song)
    {
        var structure = (SongStructure)song.Trace.Single(x => x.Point == TracePoints.SongForm).Value!;
        return Median(song, structure, SectionRole.Chorus) - Median(song, structure, SectionRole.Verse);
    }

    [Test]
    [Explicit]
    public async Task Report()
    {
        double[] lifts = [..TestCorpus.Measure(256, Lift).Where(x => x is not null).Select(x => x!.Value)];
        Console.WriteLine($"{lifts.Length} songs with a verse and a chorus: the chorus's melody {lifts.Average():+0.0;-0.0} semitones over the verse's, its median {lifts.Order().ElementAt(lifts.Length / 2):+0;-0}, higher in {100.0 * lifts.Count(x => x > 0) / lifts.Length:0}%, lower in {100.0 * lifts.Count(x => x < 0) / lifts.Length:0}%");
        // the lift aimed for: the chorus's energy over the verse's, by the melody's semitones a unit of it
        double[] aimed =
        [
            ..TestCorpus.Measure(256, song =>
            {
                var structure = (SongStructure)song.Trace.Single(x => x.Point == TracePoints.SongForm).Value!;
                var energies = song.Trace.Where(x => x.Point == TracePoints.SectionEnergy).GroupBy(x => x.Section).ToDictionary(x => x.Key, x => ((SectionEnergyTrace)x.First().Value!).Energy);
                var chorus = structure.Roles.Where(x => x.Value == SectionRole.Chorus && energies.ContainsKey(x.Key)).Select(x => energies[x.Key]).ToArray();
                var verse = structure.Roles.Where(x => x.Value == SectionRole.Verse && energies.ContainsKey(x.Key)).Select(x => energies[x.Key]).ToArray();
                return chorus.Length > 0 && verse.Length > 0 ? MelodyLayers.EnergyRegister * (chorus.Average() - verse.Average()) : (double?)null;
            }).Where(x => x is not null).Select(x => x!.Value)
        ];
        Console.WriteLine($"aimed: {aimed.Average():+0.0;-0.0} semitones, over 0 in {100.0 * aimed.Count(x => x > 0) / aimed.Length:0}%");
        await Task.CompletedTask;
    }
}

using System.Collections.Immutable;
using Rmg.Core;
using Rmg.Core.Composition;
using Rmg.Core.Songs;

namespace Rmg.Tests.DrumKits;

/// <summary>
///     Whether a song's drums are heard: how much of a song of the kit and percussion plays percussion only, by its groove,
///     whether its kit plays at all, and the drums a song has that never sound, drawn or given.
/// </summary>
public sealed class DrumsHeardReportTest
{
    private static bool[] PercussionOnly(CorpusSong song) =>
        [..song.Trace.Where(x => x.Point == TracePoints.PercussionOnly).Select(x => (bool)x.Value!)];

    /// <summary>The drums a song with drums has and never sounds, those that groove apart from those of the fills and landings.</summary>
    private static (string[] Groove, string[] Other) Silent(CorpusSong song)
    {
        if (!song.Song.Draws!.Parts.Contains(TrackRole.Drum))
            return ([], []);
        var silent = song.Song.TrackDefinitions.Keys.Where(x => x >= DrumGroups.FirstTrackNumber)
            .Where(x => !song.Song.Notes!.TryGetValue(x, out var notes) || notes.Count == 0)
            .Select(DrumGroups.GetDrum).ToArray();
        return ([..silent.Where(Grooves).Select(x => x.Name)], [..silent.Where(x => !Grooves(x)).Select(x => x.Name)]);
    }

    private static bool Grooves(PercussionInstrumentDefinition drum) => DrumGroups.GroupOf(drum).GrooveChance >= 1;

    private static string Count(IEnumerable<string[]> silent)
    {
        var all = silent.ToArray();
        return $"{all.Count(x => x.Length > 0)} ({string.Join(", ", all.SelectMany(x => x).GroupBy(x => x).OrderByDescending(x => x.Count()).Select(x => $"{x.Key} {x.Count()}"))})";
    }

    private static void Describe(string what, IReadOnlyList<CorpusSong> songs)
    {
        var mixed = songs.Where(x => x.Song.Draws!.DrumSetup == DrumSetup.KitAndPercussion && x.Song.Draws.DrumPieces.Count(DrumPieces.IsPiece) > 0)
            .Where(x => PercussionOnly(x).Length > 0).ToArray();
        var shares = mixed.Select(x => PercussionOnly(x).Count(y => y) / (double)PercussionOnly(x).Length).ToArray();
        var kitless = mixed.Count(x => PercussionOnly(x).All(y => y));
        var silent = songs.Select(Silent).ToArray();
        Console.WriteLine($"{what}: {songs.Count} songs, {songs.Count(x => !x.Song.Draws!.Parts.Contains(TrackRole.Drum))} drumless, {mixed.Length} of the kit and percussion; " +
                          $"sections percussion only {(shares.Length > 0 ? shares.Average() : 0):P0}, kit never {kitless}");
        Console.WriteLine($"    songs with a drum of the groove never heard {Count(silent.Select(x => x.Groove))}");
        Console.WriteLine($"    songs with a drum of the fills and landings never heard {Count(silent.Select(x => x.Other))}");
        foreach (var (low, high) in new[] { (0.0, 0.33), (0.33, 0.67), (0.67, 1.01) })
        {
            var band = mixed.Where(x => x.Song.Draws!.Unconventionality[Facet.Groove] is var g && g >= low && g < high).ToArray();
            if (band.Length > 0)
                Console.WriteLine($"    groove {low:F2}-{high:F2}: {band.Length} songs, percussion only {band.Average(x => PercussionOnly(x).Count(y => y) / (double)PercussionOnly(x).Length):P0}, kit never {band.Count(x => PercussionOnly(x).All(y => y))}");
        }
    }

    [Test]
    [Explicit]
    public async Task WhyADrumIsNotHeard()
    {
        var songs = TestCorpus.Range(256).ToArray();
        var (unpicked, pickedNotHeard, pool, drumless, sectionsWithout, sections, distinct) = (0, 0, 0, 0, 0, 0, 0);
        var poolSizes = new List<int>();
        foreach (var song in songs)
        {
            var drums = song.Song.TrackDefinitions.Keys.Where(x => x >= DrumGroups.FirstTrackNumber).ToArray();
            if (drums.Length == 0 || drums.All(x => !song.Song.Notes!.TryGetValue(x, out var n) || n.Count == 0)) { drumless++; continue; }
            var kits = song.Trace.Where(x => x.Point == TracePoints.Kit).ToDictionary(x => x.Section, x => (SectionKit)x.Value!);
            distinct += kits.Count;
            var picked = kits.Values.SelectMany(x => x.Drums).Select(DrumGroups.GetTrackNumber).ToHashSet();
            pool += drums.Length;
            poolSizes.Add(drums.Length);
            foreach (var drum in drums)
            {
                var heard = song.Song.Notes!.TryGetValue(drum, out var n) && n.Count > 0;
                if (!picked.Contains(drum)) unpicked++;
                else if (!heard) pickedNotHeard++;
            }
            foreach (var span in song.Map.Sections)
            {
                sections++;
                if (!drums.Any(d => song.Song.Notes!.TryGetValue(d, out var n) && n.Any(x => x.Position >= span.Start && x.Position < span.End))) sectionsWithout++;
            }
        }
        Console.WriteLine($"{songs.Length} songs, drumless {drumless}; pool drums {pool} ({poolSizes.Average():F1} a song, {poolSizes.Min()}-{poolSizes.Max()}); " +
                          $"never picked by a section {unpicked}, picked but never heard {pickedNotHeard}; kits drawn {distinct} ({distinct / (double)(songs.Length - drumless):F1} a song); " +
                          $"sections with no drum {sectionsWithout} of {sections}");
        await Task.CompletedTask;
    }

    [Test]
    [Explicit]
    public async Task Report()
    {
        Describe("Drawn", [..TestCorpus.Range(256)]);
        string[][] sets = [["kick", "bongo", "conga"], ["kick", "acousticSnare", "hiHat", "bongo", "conga"], ["kick", "bongo", "conga", "timbale", "agogo"]];
        foreach (var pieces in sets)
        {
            Describe(string.Join("+", pieces), [..Enumerable.Range(0, 64).Select(seed => TestCorpus.Get(seed, new SongOverrides(DrumPieces: [..pieces])))]);
            Describe(string.Join("+", pieces) + ", groove at 1", [..Enumerable.Range(0, 64).Select(seed =>
                TestCorpus.Get(seed, new SongOverrides(DrumPieces: [..pieces], Facets: ImmutableDictionary<Facet, double>.Empty.Add(Facet.Groove, 1))))]);
        }

        await Task.CompletedTask;
    }
}

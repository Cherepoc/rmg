using System.Collections.Immutable;
using Rmg.Core.Composition;
using Rmg.Core.Songs;

namespace Rmg.Tests.Genres;

/// <summary>A genre's mood as its harmony makes it: its sections' scales, its chords' degrees, and how long its chords sound major or minor.</summary>
public sealed class MoodReportTest
{
    /// <summary>A chord's quality by its pitch classes: major or minor where a triad of either is in it, the major first, else other.</summary>
    private static string Quality(IEnumerable<int> pitches)
    {
        var classes = pitches.Select(x => x % 12).ToHashSet();
        if (classes.Count < 3)
            return "two notes or fewer";
        bool Has(int root, int third) => classes.Contains(root) && classes.Contains((root + third) % 12) && classes.Contains((root + 7) % 12);
        if (Enumerable.Range(0, 12).Any(r => Has(r, 3)) && Enumerable.Range(0, 12).Any(r => Has(r, 4)))
            return "both";
        return Enumerable.Range(0, 12).Any(r => Has(r, 4)) ? "major" : Enumerable.Range(0, 12).Any(r => Has(r, 3)) ? "minor" : "other";
    }

    private static void Describe(string label, IReadOnlyList<CorpusSong> songs)
    {
        var scales = songs.SelectMany(x => x.Trace.Where(t => t.Point == TracePoints.SectionScale).Select(t => ((Scale)t.Value!).Name)).ToArray();
        var drawn = songs.SelectMany(x => x.Trace.Where(t => t.Point == TracePoints.Progression).Select(t => (ImmutableArray<int>)t.Value!)).SelectMany(x => x.Select(r => (r % 7 + 7) % 7)).ToArray();
        var chords = songs.SelectMany(x => x.Song.Notes!.Where(n => x.Song.TrackDefinitions[n.Key].Role is TrackRole.Chords or TrackRole.Pad).SelectMany(n => n.Value))
            .Where(x => x.Value.Pitches.Length >= 3).Select(x => (Quality: Quality(x.Value.Pitches), x.Value.Duration)).ToArray();
        var total = chords.Sum(x => x.Duration);
        Console.WriteLine($"{label}: scales {string.Join(", ", scales.GroupBy(x => x).OrderByDescending(x => x.Count()).Select(x => $"{x.Key} {x.Count() / (double)scales.Length:P0}"))}");
        Console.WriteLine($"{label}: degrees {string.Join(", ", drawn.GroupBy(x => x).OrderBy(x => x.Key).Select(x => $"{x.Key + 1}: {x.Count() / (double)drawn.Length:P0}"))}");
        var kinds = songs.SelectMany(x => x.Trace.Where(t => t.Point == TracePoints.ChordScales).SelectMany(t => (IEnumerable<ChordScale>)t.Value!)).Select(x => x.Kind).ToArray();
        Console.WriteLine($"{label}: chords' scales {string.Join(", ", kinds.GroupBy(x => x).OrderByDescending(x => x.Count()).Select(x => $"{x.Key} {x.Count() / (double)kinds.Length:P0}"))}");
        Console.WriteLine($"{label}: chords sounding {string.Join(", ", chords.GroupBy(x => x.Quality).OrderByDescending(x => x.Sum(c => c.Duration)).Select(x => $"{x.Key} {x.Sum(c => c.Duration) / total:P0}"))}");
    }

    /// <summary>
    ///     How long a song's chords and pads sound major, of the chords with a third: each note's chord by its state, a major
    ///     third over its root or a minor, whatever its part strikes of it, so that a seventh is its triad's and a chord
    ///     broken into single notes is counted; a sus or a power chord, which has no third, is left out.
    /// </summary>
    private static double Major(CorpusSong song)
    {
        var chords = song.Song.Notes!.Where(n => song.Song.TrackDefinitions[n.Key].Role is TrackRole.Chords or TrackRole.Pad).SelectMany(n => n.Value)
            .Select(x => (Chord: Realizer.GetChordNotes(x.Value.State), x.Value.Duration))
            .Select(x => (Root: x.Chord.Chord.GetPitch(0), x.Chord.Classes, x.Duration))
            .Select(x => (Major: x.Classes.Contains(((x.Root + 4) % 12 + 12) % 12), Minor: x.Classes.Contains(((x.Root + 3) % 12 + 12) % 12), x.Duration))
            .Where(x => x.Major != x.Minor).ToArray();
        return chords.Length > 0 ? chords.Where(x => x.Major).Sum(x => x.Duration) / chords.Sum(x => x.Duration) : double.NaN;
    }

    /// <summary>
    ///     Every preset's mood at its ends, the same seeds at darkness -1, 0 and 1 given, its other setting at the middle, and
    ///     at intensity -1, 0 and 1 likewise: the darkness by how long its chords sound major and its sections' scales'
    ///     darkness, the intensity by its tempo and how many notes a second its pitched parts play; so that a setting
    ///     whose ends a genre plays alike shows by number (the review reads it, run/mood.txt).
    /// </summary>
    [Test]
    [Explicit]
    public async Task Ends()
    {
        const int count = 16;
        double[] ends = [-1, 0, 1];
        var presets = new (string Name, Genre? Genre)[] { ("none", null) }.Concat(GenrePresets.All.Select(x => (x.Name, (Genre?)GenrePresets.Genre(x)))).ToArray();
        Console.WriteLine($"{"",-20}{"major chords",24}{"scale darkness",24}{"tempo",24}{"notes a second",24}");
        Console.WriteLine($"{"",-20}" + string.Concat(Enumerable.Repeat($"{"-1",8}{"0",8}{"1",8}", 4)));
        foreach (var (name, genre) in presets)
        {
            double Mean(IEnumerable<double> values) => values.Where(double.IsFinite).DefaultIfEmpty(double.NaN).Average();
            CorpusSong[] At(double? darkness, double? intensity) =>
                TestCorpus.InParallel(Enumerable.Range(0, count), seed => TestCorpus.Get(seed, new SongOverrides(Genre: genre, Darkness: darkness, Intensity: intensity))).ToArray();
            var dark = ends.Select(x => At(x, 0)).ToArray();
            var fierce = ends.Select(x => At(0, x)).ToArray();
            string Row(Func<CorpusSong, double> measure, CorpusSong[][] songs, string format) => string.Concat(songs.Select(x => Mean(x.Select(measure)).ToString(format).PadLeft(8)));
            Console.WriteLine($"{name,-20}" +
                              Row(Major, dark, "P0") +
                              Row(x => Mean(x.Trace.Where(t => t.Point == TracePoints.SectionScale).Select(t => ((Scale)t.Value!).Darkness)), dark, "0.00") +
                              Row(x => x.Song.TrackEventStateTimelineMap.CommonStateTimelineMap.GetStateTimeline(Rmg.Core.Events.StateKinds.Tempo).GetEffectiveValueAt(0) * Meter.BaseTempo, fierce, "0") +
                              Row(x => x.Song.Notes!.Where(n => x.Song.TrackDefinitions[n.Key] is PitchInstrumentTrack).Sum(n => n.Value.Count) / x.Rendered.Seconds, fierce, "0.0"));
        }

        await Task.CompletedTask;
    }

    [Test]
    [Explicit]
    public async Task Report()
    {
        foreach (var name in new[] { "blackMetal", "idm", "metal", "djent", "nuMetal", "dungeon", "trap", "dnb", "techno", "medieval", "blues", "pop" })
        {
            Describe(name, TestCorpus.InParallel(Enumerable.Range(0, 64), seed => TestCorpus.Get(seed, new SongOverrides(Genre: GenrePresets.Genre(name)))));
        }

        await Task.CompletedTask;
    }
}

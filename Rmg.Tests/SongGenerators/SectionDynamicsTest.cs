using Rmg.Core;
using Rmg.Core.Composition;
using Rmg.Core.Songs;

namespace Rmg.Tests.SongGenerators;

/// <summary>
///     How the sections of a song differ: their loudness, the drums' notes and how many drums play, and the other
///     tracks' notes, each section as it last plays, leaving out the last bar of each phrase in its own meter, where the fills are.
/// </summary>
public sealed class SectionDynamicsTest
{
    private const int SongCount = 256;

    private static readonly string[] Measures = ["Loudness", "Drum notes", "Drums", "Bass notes", "Chord notes", "Melody notes"];

    /// <summary>
    ///     How much louder each section's last appearance plays than its first, as rendered, every pitched part's notes
    ///     but the soloists', in the first pattern of each; over a song's sections that play more than once.
    /// </summary>
    private static double[] Growth(CorpusSong song)
    {
        double Loudness(SectionSpan span) =>
            song.Song.Notes!.Where(x => song.Song.TrackDefinitions[x.Key] is PitchInstrumentTrack).SelectMany(x => x.Value)
                .Where(x => x.Position >= span.Start && x.Position < span.Start + span.Meter.PatternDuration - span.Meter.BarDuration && x.Value.State.GetStateValue(CompositionStateKinds.LineSolo) == 0)
                .Select(x => Rmg.Core.Rendering.Render.ToMidiVelocity(x.Value.Velocity)).DefaultIfEmpty(double.NaN).Average();
        return
        [
            ..song.Map.Sections.Select((x, i) => (x, i)).Where(x => !song.Solos.ContainsKey(x.i)).GroupBy(x => x.x.SectionId).Where(x => x.Count() > 1)
                .Select(x => Loudness(x.Last().x) - Loudness(x.First().x)).Where(x => !double.IsNaN(x))
        ];
    }

    [Test]
    public async Task ASectionsLastAppearance_PlaysLouderThanItsFirst()
    {
        var growth = TestCorpus.Measure(256, Growth).SelectMany(x => x).ToArray();

        // seen over three corpora: louder by 0.080 to 0.085, louder in 95% to 97%
        Console.WriteLine($"{growth.Length} sections played again: louder by {growth.Average():F3} on the average, louder in {growth.Count(x => x > 0) / (double)growth.Length:P0}");
        await Assert.That(growth.Length).IsGreaterThan(100);
        // louder the last time, a section growing as it comes back, on the average and in most of them
        await Assert.That(growth.Average()).IsGreaterThan(0.05);
        await Assert.That(growth.Count(x => x > 0) / (double)growth.Length).IsGreaterThan(0.8);
    }

    [Test]
    [Explicit]
    public async Task Report()
    {
        var songs = TestCorpus.Range(SongCount).Select(MeasureSections).ToArray();

        Console.WriteLine("Spread between sections over the bar-to-bar spread within one:");
        for (var m = 0; m < Measures.Length; m++)
            Console.WriteLine($"  {Measures[m]}: {SpreadRatio(songs, m):F2}");

        Console.WriteLine("Correlation between sections, within songs: all, plain, wild:");
        for (var a = 0; a < Measures.Length; a++)
        for (var b = a + 1; b < Measures.Length; b++)
            Console.WriteLine($"  {Measures[a]} ~ {Measures[b]}: {Correlations(songs, x => x.Values[a], x => x.Values[b])}");
        for (var m = 0; m < Measures.Length; m++)
            Console.WriteLine($"  Energy ~ {Measures[m]}: {Correlations(songs, x => x.Energy, x => x.Values[m])}");

        var lines = TestCorpus.Range(SongCount).SelectMany(ReadSectionChanges).ToArray();
        Console.WriteLine($"Fills at section changes, by the energy they lead into ({lines.Length} lines):");
        Console.WriteLine($"  Lift ~ span: {Pearson([..lines.Select(x => (x.Lift, x.Span))]):F2}");
        Console.WriteLine($"  Lift ~ fullness of the fills: {Pearson([..lines.Where(x => x.Fullness is not null).Select(x => (x.Lift, x.Fullness!.Value))]):F2}");
        foreach (var (name, from, to) in new[] { ("into less", -9.0, -0.25), ("even", -0.25, 0.25), ("into more", 0.25, 9.0) })
        {
            var group = lines.Where(x => x.Lift >= from && x.Lift < to).ToArray();
            Console.WriteLine(
                $"  {name}: {group.Length} lines, span {group.Average(x => x.Span):F2}, none {group.Count(x => x.Span == 0) / (double)group.Length:P0}, " +
                $"stopped {group.Count(x => x.Stopped) / (double)group.Length:P0}, landing {group.Count(x => x.Lands) / (double)group.Length:P0}"
            );
        }

        await Task.CompletedTask;
    }

    private static string Correlations(SectionMeasures[][] songs, Func<SectionMeasures, double> a, Func<SectionMeasures, double> b)
    {
        return $"{Correlate(songs, a, b):F2}, {Correlate(songs, a, b, x => x.Unconventionality < 0.4):F2}, " +
               $"{Correlate(songs, a, b, x => x.Unconventionality > 0.6):F2}";
    }

    /// <summary>A fill before a section change: the energy it leads into over the one it ends, and what it played.</summary>
    internal sealed record SectionChange(double Lift, double Span, double? Fullness, bool Stopped, bool Lands);

    internal static IEnumerable<SectionChange> ReadSectionChanges(CorpusSong song)
    {
        // the lines at section changes are those whose sections differ, which are the only ones to lead into energy
        return song.Trace
            .Select(x => x.Value)
            .OfType<FillDecision>()
            .Where(x => x.Lift != 0)
            .Select(x => new SectionChange(
                    x.Lift,
                    x.Span,
                    x.Span > 0 ? x.Fullness : null,
                    x.Span > 0 && x.Treatment == GrooveTreatment.Stop,
                    !x.Landing.IsEmpty
                )
            );
    }

    /// <summary>A section's measures over its bars, as it last plays in the song, with its energy and how far its rhythm strays.</summary>
    internal sealed record SectionMeasures(int SectionId, double[][] Bars, double Energy, double Unconventionality)
    {
        public double[] Values => [..Enumerable.Range(0, Bars[0].Length).Select(m => Bars.Average(x => x[m]))];
    }

    internal static SectionMeasures[] MeasureSections(CorpusSong song)
    {
        var notes = song.Song.Notes!;
        // every section's energy and its groove facet, as the trace records them
        var energies = song.Trace.Where(x => x.Point == TracePoints.SectionEnergy).ToDictionary(x => x.Section, x => ((SectionEnergyTrace)x.Value!).Energy);
        var grooves = song.Trace.Where(x => x.Point == TracePoints.SectionUnconventionality).ToDictionary(x => x.Section, x => ((Unconventionality)x.Value!).Facets[Facet.Groove]);
        return song.Map.Sections
            .GroupBy(x => x.SectionId)
            .Select(x => x.Last())
            .Select(span =>
                {
                    return new SectionMeasures(span.SectionId, [..Bars(span).Select(bar => MeasureBar(song.Song, notes, bar, span.Meter))], energies[span.SectionId], grooves[span.SectionId]);
                }
            )
            .ToArray();
    }

    /// <summary>The bars of a section where it plays, but the last of each phrase.</summary>
    private static IEnumerable<double> Bars(SectionSpan span)
    {
        for (var bar = 0; bar * span.Meter.BarDuration < span.Duration; bar++)
            if (bar % span.Meter.PhraseBars != span.Meter.PhraseBars - 1)
                yield return span.Start + bar * span.Meter.BarDuration;
    }

    private static double[] MeasureBar(Song song, IReadOnlyDictionary<int, Core.Events.EventTimeline<RealizedNote>> notes, double start, Meter meter)
    {
        var end = start + meter.BarDuration;
        var inBar = notes.ToDictionary(x => x.Key, x => x.Value.Where(n => n.Position >= start - 1e-9 && n.Position < end - 1e-9).ToArray());
        // the notes on the beat, which every track accents alike, so that a busier bar is not a quieter one
        var velocities = inBar.Values.SelectMany(x => x)
            .Where(x => song.PlaceOf(x.Position) is var at && at.Meter.Pulses.Any(p => Math.Abs(at.Beat - p) < 1e-6))
            .Select(x => x.Value.Velocity)
            .ToArray();
        var drums = inBar.Where(x => song.IsDrum(x.Key)).ToArray();
        return
        [
            velocities.Length == 0 ? 0 : velocities.Average(),
            drums.Sum(x => x.Value.Length),
            drums.Count(x => x.Value.Length > 0),
            inBar.GetValueOrDefault(song.Track(TrackRole.Bass))?.Length ?? 0,
            inBar.GetValueOrDefault(song.Track(TrackRole.Chords))?.Length ?? 0,
            inBar.GetValueOrDefault(song.Track(TrackRole.Melody))?.Length ?? 0
        ];
    }

    /// <summary>
    ///     How much sections differ in a measure against how much the bars of one do: the root of the mean variance of
    ///     the sections' means within a song over the root of the mean variance of the bars within a section.
    /// </summary>
    internal static double SpreadRatio(IEnumerable<SectionMeasures[]> songs, int measure)
    {
        var between = new List<double>();
        var within = new List<double>();
        foreach (var sections in songs.Where(x => x.Length >= 2))
        {
            between.Add(Variance(sections.Select(x => x.Values[measure])));
            within.AddRange(sections.Select(x => Variance(x.Bars.Select(b => b[measure]))));
        }

        return Math.Sqrt(between.Average()) / Math.Sqrt(within.Average());
    }

    /// <summary>
    ///     The correlation of two measures between the sections of a song, pooled over the songs: each measure is taken
    ///     as its distance from the song's mean in the song's standard deviations, so that songs are compared by how
    ///     their sections differ, not by how they differ from each other.
    /// </summary>
    internal static double Correlate(
        IEnumerable<SectionMeasures[]> songs,
        Func<SectionMeasures, double> a,
        Func<SectionMeasures, double> b,
        Func<SectionMeasures, bool>? where = null
    )
    {
        var pairs = new List<(double A, double B)>();
        foreach (var sections in songs.Where(x => x.Length >= 3))
        {
            var za = Standardize(sections.Select(a).ToArray());
            var zb = Standardize(sections.Select(b).ToArray());
            if (za is null || zb is null)
                continue;
            pairs.AddRange(sections.Select((x, i) => (x, i)).Where(x => where?.Invoke(x.x) ?? true).Select(x => (za[x.i], zb[x.i])));
        }

        return Pearson(pairs);
    }

    internal static double Pearson(IReadOnlyList<(double A, double B)> pairs)
    {
        if (pairs.Count < 2)
            return double.NaN;
        var meanA = pairs.Average(x => x.A);
        var meanB = pairs.Average(x => x.B);
        var cov = pairs.Sum(x => (x.A - meanA) * (x.B - meanB));
        var varA = pairs.Sum(x => Math.Pow(x.A - meanA, 2));
        var varB = pairs.Sum(x => Math.Pow(x.B - meanB, 2));
        return cov / Math.Sqrt(varA * varB);
    }

    private static double[]? Standardize(double[] values)
    {
        var mean = values.Average();
        var sd = Math.Sqrt(Variance(values));
        return sd < 1e-9 ? null : [..values.Select(x => (x - mean) / sd)];
    }

    private static double Variance(IEnumerable<double> values)
    {
        var array = values.ToArray();
        var mean = array.Average();
        return array.Average(x => Math.Pow(x - mean, 2));
    }
}

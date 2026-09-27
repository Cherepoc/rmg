using Rmg.Core;
using Rmg.Core.Composition;
using Rmg.Core.Songs;

namespace Rmg.Tests.Fills;

/// <summary>
///     How the fills that play notes of their own sit against the groove they end, over the corpus: the grid they play
///     on and how loud they are, against the drums but the kick in the bar before each fill.
/// </summary>
public sealed class FillGrooveTest
{
    private const int SongCount = 200;

    private sealed record Measured(string Kind, double FillGrid, double GrooveGrid, bool IsOffTheGroove, double FillVelocity, double GrooveVelocity);

    private static readonly Lazy<Measured[]> Fills = new(Measure);

    /// <summary>The smallest step between the positions, or NaN for fewer than two.</summary>
    private static double Grid(IEnumerable<double> positions)
    {
        var sorted = positions.Distinct().Order().ToArray();
        return sorted.Length < 2 ? double.NaN : sorted.Zip(sorted.Skip(1), (a, b) => Math.Round(b - a, 4)).Min();
    }

    private static Measured[] Measure()
    {
        var kick = DrumGroups.GetTrackNumber(DrumDefinitions.Kick);
        var measured = new List<Measured>();
        foreach (var song in TestCorpus.Range(SongCount))
        {
            // a line at every pattern from the first section on, the first after the intro only if it has one, and the
            // last before the ending only if it has one
            var map = song.Map;
            var decisions = song.Trace.Where(x => x.Point == "Fill decision").ToArray();
            var first = map.Intro.Duration > 0 ? 0 : 1;
            var last = (int)Math.Round((map.Sections[^1].End - song.Origin) / Meter.PatternDuration) - (map.Ending.Kind == EndingKind.Open ? 1 : 0);
            var lines = Enumerable.Range(first, last - first + 1).Select(x => song.Origin + x * Meter.PatternDuration).ToArray();
            if (lines.Length != decisions.Length)
                throw new InvalidOperationException($"seed {song.Seed}: {lines.Length} lines, {decisions.Length} fill decisions");

            var notes = song.Song.Notes!;
            for (var i = 0; i < lines.Length; i++)
            {
                var parts = decisions[i].Phrase!.Split(", ");
                var kind = parts[0];
                if (!FillLayers.Specs.TryGetValue(Enum.Parse<FillKind>(kind), out var spec) || spec.Voices.IsEmpty)
                    continue;

                var span = double.Parse(parts[1].Split(' ')[0]);
                var from = lines[i] - span;
                (int Track, double Position, double Velocity)[] Hands(double a, double b) =>
                [
                    ..notes.Where(x => x.Key >= DrumGroups.FirstTrackNumber && x.Key != kick)
                        .SelectMany(x => x.Value.Where(n => n.Position >= a - 1e-6 && n.Position < b - 1e-6)
                            .Select(n => (x.Key, n.Position, n.Value.Velocity)))
                ];

                var fill = Hands(from, lines[i]);
                var groove = Hands(from - Meter.BarDuration, from);
                if (fill.Length == 0 || groove.Length == 0)
                    continue;

                var grooveGrid = Grid(groove.Select(x => x.Position));
                // every drum's own grid, from its first note, which a fill's note is on if it falls on a halving of it,
                // down to an eighth, and off if it falls on none of them, as 16ths over triplets
                var drumGrids = groove.GroupBy(x => x.Track)
                    .Select(x => (Start: x.Min(y => y.Position), Grid: Grid(x.Select(y => y.Position))))
                    .Where(x => !double.IsNaN(x.Grid))
                    .ToArray();
                var isOff = drumGrids.Length > 0 && fill.Any(x => drumGrids.All(g =>
                        {
                            var steps = (x.Position - g.Start) / (g.Grid / 8);
                            return Math.Abs(steps - Math.Round(steps)) > 1e-3;
                        }
                    )
                );
                measured.Add(new Measured(
                    kind,
                    Grid(fill.Select(x => x.Position)),
                    grooveGrid,
                    isOff,
                    fill.Average(x => x.Velocity),
                    groove.Average(x => x.Velocity)
                ));
            }
        }

        return [..measured];
    }

    private static string Describe(IEnumerable<Measured> fills)
    {
        var all = fills.ToArray();
        var withGrids = all.Where(x => !double.IsNaN(x.FillGrid) && !double.IsNaN(x.GrooveGrid)).ToArray();
        return $"n={all.Length}, finer {withGrids.Count(x => x.FillGrid < x.GrooveGrid * 0.99) * 100.0 / withGrids.Length:F0}%, "
               + $"4x finer {withGrids.Count(x => x.FillGrid <= x.GrooveGrid / 4 + 1e-6) * 100.0 / withGrids.Length:F0}%, "
               + $"off {all.Count(x => x.IsOffTheGroove) * 100.0 / all.Length:F0}%, "
               + $"velocity {all.Average(x => x.FillVelocity):F3} vs {all.Average(x => x.GrooveVelocity):F3}";
    }

    [Test]
    public async Task Measurement()
    {
        var fills = Fills.Value;
        if (Environment.GetEnvironmentVariable("RMG_MEASURE") is { } path)
            await File.WriteAllLinesAsync(
                path,
                [..fills.GroupBy(x => x.Kind).Select(x => $"{x.Key}: {Describe(x)}"), $"all: {Describe(fills)}"]
            );

        await Assert.That(fills.Length).IsGreaterThan(1000);
    }

    [Test]
    public async Task Fills_PlayNearTheGroovesGrid()
    {
        var fills = Fills.Value.Where(x => !double.IsNaN(x.FillGrid) && !double.IsNaN(x.GrooveGrid)).ToArray();

        // a rank or two finer than the snare, which the groove's other drums often are already
        await Assert.That(fills.Count(x => x.FillGrid <= x.GrooveGrid / 4 + 1e-6)).IsLessThan(fills.Length / 20);
        await Assert.That(fills.Count(x => x.FillGrid < x.GrooveGrid * 0.99)).IsLessThan(fills.Length / 3);
    }

    [Test]
    public async Task Fills_FallOnTheGroovesGrid()
    {
        var fills = Fills.Value;

        // off it where a twist plays a tuplet, or the snare's feel is not the other drums'
        await Assert.That(fills.Count(x => x.IsOffTheGroove)).IsLessThan(fills.Length / 8);
    }

    [Test]
    public async Task Fills_PlayAboutAsLoudAsTheGroove()
    {
        var fills = Fills.Value;

        await Assert.That(fills.Average(x => x.FillVelocity)).IsEqualTo(fills.Average(x => x.GrooveVelocity)).Within(0.1);
    }
}

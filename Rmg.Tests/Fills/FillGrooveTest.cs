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
    private const int SongCount = 256;

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
        foreach (var song in TestCorpus.Range(SongCount).Where(x => x.PlaysDrums))
        {
            // a line at every pattern from the first section on, the first after the intro only if it has one, and the
            // last before the ending only if it has one
            var map = song.Map;
            var decisions = song.Trace.Where(x => x.Point == TracePoints.FillDecision).ToArray();
            var lines = song.FillLines();
            if (lines.Length != decisions.Length)
                throw new InvalidOperationException($"seed {song.Seed}: {lines.Length} lines, {decisions.Length} fill decisions");

            var notes = song.Song.Notes!;
            for (var i = 0; i < lines.Length; i++)
            {
                // the fills that play: a span, and a run that does not rest
                var decision = (FillDecision)decisions[i].Value!;
                var span = decision.Span;
                if (span <= 0 || decision.Rests)
                    continue;

                var kind = decision.Treatment.ToString();
                var from = lines[i] - span;
                (int Track, double Position, double Velocity)[] Hands(double a, double b) =>
                [
                    ..notes.Where(x => song.IsDrum(x.Key) && x.Key != kick)
                        .SelectMany(x => x.Value.Where(n => n.Position >= a - 1e-6 && n.Position < b - 1e-6)
                            .Select(n => (x.Key, n.Position, n.Value.Velocity)))
                ];

                var fill = Hands(from, lines[i]);
                var groove = Hands(from - map.MeterAt(from).BarDuration, from);
                if (fill.Length == 0 || groove.Length == 0)
                    continue;

                var grooveGrid = Grid(groove.Select(x => x.Position));
                // every drum's own grid, from its first note, which a fill's note is on if it falls on a halving of it,
                // down to an eighth, and off if it falls on none of them; straight 16ths over a triplet groove's grid fall on
                // its finer halvings, so that this tells a straight groove's fills alone
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
                    decision.Tuplet != 1 ? $"{kind} in a tuplet" : kind,
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

    /// <summary>The fills against their grooves, by kind and in all.</summary>
    [Test]
    [Explicit]
    public async Task Report()
    {
        var fills = Fills.Value;
        foreach (var line in fills.GroupBy(x => x.Kind).Select(x => $"{x.Key}: {Describe(x)}").Append($"all: {Describe(fills)}"))
            Console.WriteLine(line);
        await Task.CompletedTask;
    }

    [Test]
    public async Task Fills_PlayNearTheGroovesGrid()
    {
        await Assert.That(Fills.Value.Length).IsGreaterThan(1000);
        var fills = Fills.Value.Where(x => !double.IsNaN(x.FillGrid) && !double.IsNaN(x.GrooveGrid)).ToArray();
        Console.WriteLine($"{fills.Count(x => x.FillGrid <= x.GrooveGrid / 4 + 1e-6) / (double)fills.Length:P1} of the fills 4x finer than their groove");

        // finer than the snare's backbeat, which the groove's other drums often are already, or sparser where it folds;
        // before the fills played the groove's rhythm, a third were (measured 5.9% to 6.4%, at offsets 0 and a million)
        await Assert.That(fills.Count(x => x.FillGrid <= x.GrooveGrid / 4 + 1e-6)).IsLessThan(fills.Length / 8);
    }

    [Test]
    public async Task StraightFills_FallOnTheGroovesGrid()
    {
        // a fill in a tuplet leaves it on purpose, where the twist plays one over a straight groove
        var fills = Fills.Value.Where(x => !x.Kind.EndsWith("in a tuplet")).ToArray();

        Console.WriteLine($"{fills.Count(x => x.IsOffTheGroove) / (double)fills.Length:P1} of the straight fills off their groove's grid");
        // off it where the snare's feel is not the other drums' (measured 6.1% to 7.3%, at offsets 0 and a million)
        await Assert.That(fills.Count(x => x.IsOffTheGroove)).IsLessThan(fills.Length / 8);
    }

    [Test]
    public async Task Fills_PlayAboutAsLoudAsTheGroove()
    {
        var fills = Fills.Value;
        Console.WriteLine($"fills' velocity {fills.Average(x => x.FillVelocity):F3}, their grooves' {fills.Average(x => x.GrooveVelocity):F3}");
        // measured 0.027 to 0.031 louder, at offsets 0 and a million

        await Assert.That(fills.Average(x => x.FillVelocity)).IsEqualTo(fills.Average(x => x.GrooveVelocity)).Within(0.1);
    }
}

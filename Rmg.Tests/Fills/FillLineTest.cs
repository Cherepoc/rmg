using Rmg.Core;
using Rmg.Core.Composition;

namespace Rmg.Tests.Fills;

/// <summary>How the fills mark the lines between sections and the lines in the middle of a section.</summary>
public sealed class FillLineTest
{
    private const int SongCount = 100;

    /// <summary>A line's fill, and whether it is the line in the middle of a section.</summary>
    internal sealed record Line(bool IsPhrase, FillDecision Decision);

    /// <summary>Every section's lines, the change into it and the one in its middle, with what their fills played.</summary>
    internal static IEnumerable<Line> ReadLines(CorpusSong song)
    {
        // the decisions come in the song's order: the intro's line, if any, then every section's, then the ending's
        var decisions = song.Trace.Where(x => x.Point == TracePoints.FillDecision).Select(x => (FillDecision)x.Value!).ToArray();
        var index = song.Map.Intro.Duration > 0 ? 1 : 0;
        for (var i = 0; i < song.Map.Sections.Length; i++)
        {
            if (i > 0)
                yield return new Line(false, decisions[index++]);
            for (var line = Meter.PatternDuration; line < song.Map.Sections[i].Duration; line += Meter.PatternDuration)
                yield return new Line(true, decisions[index++]);
        }
    }

    internal static string Describe(IReadOnlyList<FillDecision> fills)
    {
        var spans = fills.GroupBy(x => x.Span).OrderBy(x => x.Key).Select(x => $"{x.Key}: {x.Count() / (double)fills.Count:P0}");
        var played = fills.Where(x => x.Span > 0).ToArray();
        return $"{fills.Count} lines; spans {string.Join(", ", spans)}; " +
               $"kick landing {fills.Count(x => x.Landing.Any(s => s.Role == FillDrumRole.Kick)) / (double)fills.Count:P0}, " +
               $"cymbal {fills.Count(x => x.Landing.Any(s => s.Role == FillDrumRole.Cymbal)) / (double)fills.Count:P0}; " +
               $"fullness {(played.Length > 0 ? played.Average(x => x.Fullness) : 0):F2}";
    }

    [Test]
    public async Task PhraseLines_WeighLess_FewerFillsSparserAndFewerLandings()
    {
        var lines = TestCorpus.Range(40).SelectMany(ReadLines).ToArray();
        FillDecision[] Of(bool isPhrase) => [..lines.Where(x => x.IsPhrase == isPhrase).Select(x => x.Decision)];
        var (phrase, section) = (Of(true), Of(false));
        double None(FillDecision[] fills) => fills.Count(x => x.Span <= 0) / (double)fills.Length;
        double Kick(FillDecision[] fills) => fills.Count(x => x.Landing.Any(s => s.Role == FillDrumRole.Kick)) / (double)fills.Length;
        double Fullness(FillDecision[] fills) => fills.Where(x => x.Span > 0).Average(x => x.Fullness);

        // the groove mostly runs on at a phrase line, as it did in a table of its own: 70% none, landing now and then
        await Assert.That(None(phrase)).IsBetween(0.6, 0.8);
        await Assert.That(None(section)).IsLessThan(0.35);
        await Assert.That(Kick(phrase)).IsLessThan(0.25);
        await Assert.That(Kick(section)).IsGreaterThan(0.6);
        await Assert.That(Fullness(phrase)).IsLessThan(Fullness(section) - 0.15);
    }

    [Test]
    [Explicit]
    public async Task Report()
    {
        var lines = TestCorpus.Range(SongCount).SelectMany(ReadLines).ToArray();
        Console.WriteLine($"Section changes: {Describe([..lines.Where(x => !x.IsPhrase).Select(x => x.Decision)])}");
        Console.WriteLine($"Phrase lines: {Describe([..lines.Where(x => x.IsPhrase).Select(x => x.Decision)])}");
        await Task.CompletedTask;
    }
}

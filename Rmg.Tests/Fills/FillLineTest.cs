using Rmg.Core;
using Rmg.Core.Composition;

namespace Rmg.Tests.Fills;

/// <summary>How the fills mark the lines between sections and the lines in the middle of a section.</summary>
public sealed class FillLineTest
{
    private const int SongCount = 128;

    /// <summary>A line's fill, and whether it is the line in the middle of a section.</summary>
    internal sealed record Line(bool IsPhrase, FillDecision Decision);

    /// <summary>Every section's lines, the change into it and those in its middle, with what their fills played.</summary>
    internal static IEnumerable<Line> ReadLines(CorpusSong song)
    {
        // the decisions come in the song's order, a line each, the intro's and the ending's among them, which are left out
        var decisions = song.Trace.Where(x => x.Point == TracePoints.FillDecision).Select(x => (FillDecision)x.Value!).ToArray();
        var sections = song.Map.Sections;
        // a drum solo's bars, which fill as a solo does, left out already (CorpusSong.FillLines)
        foreach (var (position, decision) in song.FillLines().Zip(decisions))
        {
            if (position <= sections[0].Start + 1e-9 || position >= sections[^1].End - 1e-9)
                continue;
            yield return new Line(!sections.Any(x => Math.Abs(x.Start - position) < 1e-9), decision);
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
        var lines = TestCorpus.Range(64).SelectMany(ReadLines).ToArray();
        FillDecision[] Of(bool isPhrase) => [..lines.Where(x => x.IsPhrase == isPhrase).Select(x => x.Decision)];
        var (phrase, section) = (Of(true), Of(false));
        double None(FillDecision[] fills) => fills.Count(x => x.Span <= 0) / (double)fills.Length;
        double Kick(FillDecision[] fills) => fills.Count(x => x.Landing.Any(s => s.Role == FillDrumRole.Kick)) / (double)fills.Length;
        double Fullness(FillDecision[] fills) => fills.Where(x => x.Span > 0).Average(x => x.Fullness);

        // the groove mostly runs on at a phrase line, landing now and then, against the mechanism breaking; the target, 70%
        // none, in the Report (measured: none 70% at phrases, 20% to 24% at sections; a kick landing 14% to 15% against 70% to
        // 73%; fullness 0.51 to 0.54 against 0.79 to 0.81, at offsets 0 and a million)
        Console.WriteLine($"none: phrases {None(phrase):P0}, sections {None(section):P0}; kick landing: phrases {Kick(phrase):P0}, sections {Kick(section):P0}; fullness {Fullness(phrase):F2} against {Fullness(section):F2}");
        await Assert.That(None(phrase)).IsGreaterThan(None(section) + 0.2);
        await Assert.That(Kick(phrase)).IsLessThan(Kick(section) - 0.2);
        await Assert.That(Fullness(phrase)).IsLessThan(Fullness(section) - 0.05);
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

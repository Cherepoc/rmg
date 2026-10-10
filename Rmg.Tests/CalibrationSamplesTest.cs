using System.Text.RegularExpressions;

namespace Rmg.Tests;

/// <summary>
///     The tests that print what they measure and draw fewer than 64 songs, read from the tests' sources: a calibration
///     among them measures too few (CLAUDE.md), a rule that prints a count alone does not.
/// </summary>
public sealed class CalibrationSamplesTest
{
    [Test]
    [Explicit]
    public async Task Report()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null && !Directory.Exists(Path.Combine(directory.FullName, "Rmg.Tests")))
            directory = directory.Parent;
        var tests = Path.Combine(directory?.FullName ?? throw new InvalidOperationException("No repository above the tests."), "Rmg.Tests");
        foreach (var file in Directory.GetFiles(tests, "*.cs", SearchOption.AllDirectories).Where(x => !x.Contains($"{Path.DirectorySeparatorChar}obj{Path.DirectorySeparatorChar}")))
        {
            var source = File.ReadAllText(file);
            foreach (Match test in Regex.Matches(source, @"((?:\s*\[[^\]]+\]\s*)+)public async Task (\w+)\("))
            {
                if (!test.Groups[1].Value.Contains("[Test") || test.Groups[1].Value.Contains("Explicit"))
                    continue;
                // the method's body, to its closing brace
                var (start, depth, end) = (source.IndexOf('{', test.Index + test.Length), 0, 0);
                for (end = start; end < source.Length; end++)
                    if (source[end] == '{') depth++;
                    else if (source[end] == '}' && --depth == 0) break;
                var body = source[start..end];
                var counts = Regex.Matches(body, @"(?:Enumerable\.Range\(0,\s*|TestCorpus\.Range\()(\d+)\)").Select(x => int.Parse(x.Groups[1].Value)).ToArray();
                if (body.Contains("Console.WriteLine") && counts.Length > 0 && counts.Max() < 64)
                    Console.WriteLine($"{Path.GetRelativePath(tests, file)}: {test.Groups[2].Value}, {counts.Max()} songs");
            }
        }

        await Task.CompletedTask;
    }
}

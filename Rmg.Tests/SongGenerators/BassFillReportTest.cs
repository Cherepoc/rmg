using Rmg.Core.Composition;

namespace Rmg.Tests.SongGenerators;

/// <summary>How the bass moves into a new section: by how many semitones from its last note to its first, and how often a step.</summary>
public sealed class BassFillReportTest
{
    [Test]
    public async Task AWalk_GoesByTheScalesSteps_ToAStepFromItsTarget()
    {
        int[] scale = [36, 38, 40, 41, 43, 45, 47, 48];

        await Assert.That(BassFills.Walk(scale, 36, 43, 3)).IsEquivalentTo(new[] { 38, 40, 41 });
        await Assert.That(BassFills.Walk(scale, 48, 43, 2)).IsEquivalentTo(new[] { 47, 45 });
        await Assert.That(BassFills.Walk(scale, 36, 43, 6)).IsEquivalentTo(new[] { 38, 38, 40, 40, 41, 41 });
    }

    [Test]
    [Explicit]
    public async Task Report()
    {
        var (changes, steps, leaps, moved) = (0, 0, 0, 0.0);
        foreach (var song in TestCorpus.Range(200))
        {
            var bass = song.Song.Notes![SongTracks.BassTrack].ToArray();
            foreach (var span in song.Map.Sections.Skip(1))
            {
                var last = bass.LastOrDefault(x => x.Position < span.Start - 1e-9);
                var first = bass.FirstOrDefault(x => x.Position >= span.Start - 1e-9);
                if (last.Value is null || first.Value is null || first.Position > span.Start + 1e-9 || last.Position < span.Start - 1)
                    continue;
                var move = Math.Abs(first.Value.Pitches[0] - last.Value.Pitches[0]);
                (changes, steps, leaps, moved) = (changes + 1, steps + (move is 1 or 2 ? 1 : 0), leaps + (move >= 7 ? 1 : 0), moved + move);
            }
        }

        Console.WriteLine($"{changes} changes of section with a bass note in the beat before and on the line: by step {steps / (double)changes:P0}, leaping {leaps / (double)changes:P0}, {moved / changes:F1} semitones on average");
        await Task.CompletedTask;
    }
}

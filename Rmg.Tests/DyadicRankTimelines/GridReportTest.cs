using Rmg.Core.Composition;

namespace Rmg.Tests.DyadicRankTimelines;

/// <summary>
///     Where the drums' notes fall on the grid: how many fall off both the 16ths and the 8th triplets, as the
///     quintuplets and septuplets do, how many of a drum follow its note before by less than a 32nd, heard as a rush,
///     and how many play in a bar whose drum keeps a grouped cycle, such as a tresillo.
/// </summary>
public sealed class GridReportTest
{
    [Test]
    [Explicit]
    public async Task Report()
    {
        var (notes, offGrid, rushed) = (0, 0, 0);
        foreach (var song in TestCorpus.Range(200))
        foreach (var (track, drum) in song.Song.Notes!.Where(x => x.Key >= DrumGroups.FirstTrackNumber))
        {
            var positions = drum.Select(x => x.Position).ToArray();
            notes += positions.Length;
            offGrid += positions.Count(x => !IsOn(x, 0.25) && !IsOn(x, 1 / 3.0));
            rushed += positions.Zip(positions.Skip(1), (a, b) => b - a).Count(x => x > 1e-9 && x < 0.125 - 1e-9);
        }

        Console.WriteLine($"{notes} drum notes: off the 16ths and the 8th triplets {offGrid}, under a 32nd after the one before {rushed}");
        await Task.CompletedTask;
    }

    private static bool IsOn(double position, double grid) => Math.Abs(position / grid - Math.Round(position / grid)) < 1e-6;
}

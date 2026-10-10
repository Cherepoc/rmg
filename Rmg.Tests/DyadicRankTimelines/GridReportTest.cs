using Rmg.Core.Composition;

namespace Rmg.Tests.DyadicRankTimelines;

/// <summary>
///     Where the drums' notes fall on the grid: how many fall off both the 16ths and the 8th triplets, as the
///     quintuplets and septuplets do, and how many of a drum follow its note before by less than a 32nd, heard as a rush.
/// </summary>
public sealed class GridReportTest
{
    [Test]
    [Explicit]
    public async Task Report()
    {
        var songs = TestCorpus.Measure(256, song =>
        {
            var (notes, offGrid, rushed) = (0, 0, 0);
            foreach (var (_, drum) in song.Song.Notes!.Where(x => song.IsDrum(x.Key)))
            {
                var positions = drum.Select(x => x.Position).ToArray();
                notes += positions.Length;
                // each from its bar's start, as a meter of no whole beats moves the grids
                offGrid += positions.Select(song.Map.BeatInBar).Count(x => !IsOn(x, 0.25) && !IsOn(x, 1 / 3.0));
                rushed += positions.Zip(positions.Skip(1), (a, b) => b - a).Count(x => x > 1e-9 && x < 0.125 - 1e-9);
            }

            return (Notes: notes, OffGrid: offGrid, Rushed: rushed);
        });
        var (notes, offGrid, rushed) = (songs.Sum(x => x.Notes), songs.Sum(x => x.OffGrid), songs.Sum(x => x.Rushed));

        Console.WriteLine($"{notes} drum notes: off the 16ths and the 8th triplets {offGrid}, under a 32nd after the one before {rushed}");
        await Task.CompletedTask;
    }

    private static bool IsOn(double position, double grid) => Math.Abs(position / grid - Math.Round(position / grid)) < 1e-6;
}

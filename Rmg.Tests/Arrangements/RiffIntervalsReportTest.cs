using Rmg.Core.Composition;
using Rmg.Core.Songs;
using Rmg.Tests.Genres;

namespace Rmg.Tests.Arrangements;

/// <summary>A riff's moves from note to note, a Djent band's small chromatic steps and wide dissonant leaps against a metal band's.</summary>
public sealed class RiffIntervalsReportTest
{
    /// <summary>
    ///     A preset's riffs' intervals from one note to the next, by their lowest pitch: the share repeating, a semitone, a tone,
    ///     a third to a fifth, a tritone, a major seventh or a minor ninth, and past an octave; and the share off the riff's
    ///     pedal, its most played note, that return to it.
    /// </summary>
    [Test]
    [Explicit]
    [Arguments("djent")]
    [Arguments("metal")]
    public async Task Report(string genre)
    {
        var moves = new Dictionary<string, int>();
        var (off, back) = (0, 0);
        foreach (var song in TestCorpus.Streamed(Enumerable.Range(0, 64), seed => TestCorpus.Get(seed, new SongOverrides(Genre: GenrePresets.Genre(genre)))))
        {
            if (!song.Song.TrackDefinitions.Values.Any(x => x.Role == TrackRole.Riff))
                continue;
            int[] lows = [..(song.Song.Notes!.GetValueOrDefault(song.Track(TrackRole.Riff))?.ToArray() ?? []).Where(x => x.Value.State.GetStateValue(CompositionStateKinds.Picked) <= 1).Select(x => x.Value.Pitches.Min())];
            if (lows.Length < 2)
                continue;
            var pedal = lows.GroupBy(x => x).MaxBy(x => x.Count())!.Key;
            foreach (var (a, b) in lows.Zip(lows.Skip(1)))
            {
                var step = Math.Abs(b - a);
                var name = step switch { 0 => "repeat", 1 => "semitone", 2 => "tone", 6 => "tritone", 11 or 13 => "seventh or ninth", > 12 => "past an octave", _ => "third to octave" };
                moves[name] = moves.GetValueOrDefault(name) + 1;
                if (a != pedal)
                    (off, back) = (off + 1, back + (b == pedal ? 1 : 0));
            }
        }

        var all = moves.Values.Sum();
        Console.WriteLine($"{genre}: {string.Join(", ", moves.OrderByDescending(x => x.Value).Select(x => $"{x.Key} {x.Value / (double)all:P0}"))}; off the pedal back to it {back / (double)Math.Max(1, off):P0}");
        await Task.CompletedTask;
    }
}

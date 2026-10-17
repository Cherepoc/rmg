using Rmg.Core;
using Rmg.Core.Composition;
using Rmg.Core.Songs;
using Rmg.Tests.Genres;

namespace Rmg.Tests.Arrangements;

/// <summary>
///     The melody harmonised in a block (<see cref="LineDoubling.BlockChance" />): the counter-melody stacking its chord's
///     notes under its own, a genre's.
/// </summary>
public sealed class BlockHarmonyTest
{
    private static IEnumerable<CorpusSong> Songs() =>
        new[] { "soul", "funk" }.SelectMany(genre => TestCorpus.InParallel(Enumerable.Range(0, 64), seed => TestCorpus.Get(seed, new SongOverrides(Genre: GenrePresets.Genre(genre)))));

    /// <summary>The counter-melody stacks its notes in a block only in an appearance traced as one, its doubled notes there.</summary>
    [Test]
    public async Task ACounterMelodysBlock_PlaysOnlyWhereTraced()
    {
        var blocks = 0;
        foreach (var song in Songs())
        {
            var traced = song.Trace.Where(x => x.Point == TracePoints.BlockHarmony).Select(x => (int)x.Value!).ToHashSet();
            foreach (var note in song.Song.Notes!.GetValueOrDefault(song.Track(TrackRole.CounterMelody))?.Where(x => x.Value.State.GetStateValue(CompositionStateKinds.Doubled) > 0
                         && x.Value.State.GetStateValue(CompositionStateKinds.LineVoices) >= LineDoubling.BlockVoices) ?? [])
            {
                blocks++;
                await Assert.That(song.Map.PlayingAt(note.Position) is { } play && traced.Contains(play.Index)).IsTrue().Because($"seed {song.Seed} at {note.Position}");
            }
        }

        await Assert.That(blocks).IsGreaterThan(10);
    }

    /// <summary>
    ///     A calibration: where the melody and a block's counter-melody strike together, how often the counter-melody plays
    ///     three voices with the melody, how often a note of it is a second from the melody's, and how often the melody is on
    ///     top.
    /// </summary>
    [Test]
    public async Task ABlocksVoices_StayApart()
    {
        var (blocks, together, three, seconds, melodyOnTop) = (0, 0, 0, 0, 0);
        foreach (var song in Songs())
        foreach (var index in song.Trace.Where(x => x.Point == TracePoints.BlockHarmony).Select(x => (int)x.Value!))
        {
            blocks++;
            var span = song.Map.Sections[index];
            Dictionary<double, RealizedNote> At(TrackRole role) => song.Song.Notes![song.Song.Track(role)].Where(x => x.Position >= span.Start - 1e-9 && x.Position < span.End - 1e-9)
                .GroupBy(x => Math.Round(x.Position, 6)).ToDictionary(x => x.Key, x => x.First().Value);
            var melody = At(TrackRole.Melody);
            foreach (var (position, note) in At(TrackRole.CounterMelody))
            {
                if (!melody.TryGetValue(position, out var sung))
                    continue;
                together++;
                var top = sung.Pitches.Max();
                three += note.Pitches.Length >= 2 ? 1 : 0;
                seconds += note.Pitches.Any(x => (top - x).Mod(12) is 1 or 2 or 10 or 11) ? 1 : 0;
                melodyOnTop += note.Pitches.All(x => x < top) ? 1 : 0;
            }
        }

        Console.WriteLine($"{blocks} blocks, {together} notes struck together: three voices {three / (double)together:P1}, a second from the melody " +
                          $"{seconds / (double)together:P1}, the melody on top {melodyOnTop / (double)together:P1}");
        await Assert.That(together).IsGreaterThan(200);
        await Assert.That(three / (double)together).IsGreaterThan(0.2);
        await Assert.That(seconds / (double)together).IsLessThan(0.1);
    }
}

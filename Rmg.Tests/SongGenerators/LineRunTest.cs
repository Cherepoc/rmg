using Rmg.Core.Composition;
using Rmg.Core.Songs;
using Rmg.Tests.Genres;

namespace Rmg.Tests.SongGenerators;

/// <summary>The counter-melody running into a section, an octave of the scale in 16ths, as a disco string section sweeps into its chorus.</summary>
public sealed class LineRunTest
{
    /// <summary>A run plays its seven 16ths before the line, one way all along, each a step or two from the one before.</summary>
    [Test]
    public async Task ARun_SweepsByStepsInSixteenths_IntoItsSection()
    {
        var genre = GenrePresets.With("disco", GenreChoices.LineRun, ("counterMelody", 1));
        var (runs, traced, songs) = (0, 0, 0);
        foreach (var song in TestCorpus.InParallel(Enumerable.Range(0, 64), seed => TestCorpus.Get(seed, new SongOverrides(Genre: genre))))
        {
            var counter = song.Song.Track(TrackRole.CounterMelody);
            if (!song.Song.Notes!.TryGetValue(counter, out var notes))
                continue;
            songs++;
            foreach (var (line, positions) in song.Trace.Where(x => x.Point == TracePoints.LineFill && x.Track == counter).Select(x => ((double Line, double[] Positions))x.Value!))
            {
                if (positions.Length != LineFills.RunNotes || positions.Zip(positions.Skip(1)).Any(x => Math.Abs(x.Second - x.First - LineFills.RunStep) > 1e-9))
                    continue;
                traced++;
                // the run as played, but where a later stage took a note out, as a stop does
                var played = positions.Select(p => notes.FirstOrDefault(n => Math.Abs(n.Position - p) < 0.05)).Where(x => x.Value is not null).Select(x => x.Value.Pitches[0]).ToArray();
                if (played.Length < LineFills.RunNotes)
                    continue;
                runs++;
                var steps = played.Zip(played.Skip(1), (a, b) => b - a).ToArray();
                await Assert.That(steps.All(x => x > 0) || steps.All(x => x < 0)).IsTrue().Because($"seed {song.Seed} into {line}: {string.Join(" ", played)}");
                await Assert.That(steps.All(x => Math.Abs(x) <= 4)).IsTrue().Because($"seed {song.Seed} into {line}: {string.Join(" ", played)}");
            }
        }

        Console.WriteLine($"{runs} runs played whole of {traced} drawn, in {songs} songs with a counter-melody");
        await Assert.That(runs).IsGreaterThan(10);
    }
}

using Rmg.Core;
using Rmg.Core.Composition;
using Rmg.Core.Events;
using Rmg.Core.Songs;

namespace Rmg.Tests.Forms;

/// <summary>
///     The cadenza (<see cref="EndingKind.Cadenza" />): the band holding the last chord it played, the drums out, while
///     the melody runs alone over it, then the band's last hit together.
/// </summary>
public sealed class CadenzaTest
{
    private const int SongCount = 64;

    private static IEnumerable<CorpusSong> Songs => TestCorpus.Streamed(Enumerable.Range(0, SongCount),
        seed => TestCorpus.Get(seed, new SongOverrides(Genre: TestCorpus.Always(GenreChoices.Ending, GenreChoices.Name(nameof(EndingKind.Cadenza))))));

    /// <summary>The roles that hold the chord, which strike it once, as the hold starts.</summary>
    private static readonly TrackRole[] Holding = [TrackRole.Chords, TrackRole.Pad, TrackRole.Bass];

    /// <summary>
    ///     A cadenza's drums are silent over its hold, the chords, the pad and the bass strike its chord once as it starts,
    ///     the melody runs over it, and the band strikes the last hit together after it.
    /// </summary>
    [Test]
    public async Task ACadenza_HoldsTheBand_WhileTheMelodyRunsAlone()
    {
        var endings = 0;
        foreach (var song in Songs)
        {
            await Assert.That(song.Map.Ending.Kind).IsEqualTo(EndingKind.Cadenza).Because($"seed {song.Seed}");
            endings++;
            var (start, hit) = (song.Map.Ending.Start, song.Map.Ending.Start + song.Map.Ending.Held);
            bool Within(double at) => at > start + 1e-6 && at < hit - 1e-6;
            foreach (var (track, notes) in song.Song.Notes!)
            {
                var role = song.Song.TrackDefinitions[track].Role;
                if (song.IsDrum(track))
                    await Assert.That(notes.Count(x => x.Position >= start - 1e-6 && x.Position < hit - 1e-6)).IsEqualTo(0).Because($"seed {song.Seed}, a drum in the hold");
                else if (Holding.Contains(role))
                    await Assert.That(notes.Count(x => Within(x.Position))).IsEqualTo(0).Because($"seed {song.Seed}, the {role} striking again in the hold");
            }

            if (song.Song.TrackDefinitions.Values.Any(x => x.Role == TrackRole.Melody))
                await Assert.That(song.Song.Notes![song.Track(TrackRole.Melody)].Count(x => Within(x.Position))).IsGreaterThan(2).Because($"seed {song.Seed}, the melody's run");
            await Assert.That(song.Song.Notes!.Where(x => !song.IsDrum(x.Key)).Any(x => x.Value.Any(n => Math.Abs(n.Position - hit) < 1e-6))).IsTrue().Because($"seed {song.Seed}, its last hit at {hit}");
        }

        await Assert.That(endings).IsEqualTo(SongCount);
    }

    /// <summary>
    ///     How a cadenza plays: how long its hold is, how often its held chord is the dominant, the bass a fifth above the
    ///     tonic, and of the melody's run, how many notes are its chord's, its scale's or chromatic, and how many move by a step.
    /// </summary>
    [Test]
    [Explicit]
    public async Task Report()
    {
        var (songs, dominant, beats, notes, onChord, inScale, byStep) = (0, 0, 0.0, 0, 0, 0, 0);
        foreach (var song in Songs)
        {
            songs++;
            var start = song.Map.Ending.Start;
            beats += song.Map.Ending.Held;
            var tonic = song.Song.TrackEventStateTimelineMap.CommonStateTimelineMap.GetStateTimeline(StateKinds.KeyOffset).GetEffectiveValueAt(start).Mod(12);
            if (song.Song.Notes!.GetValueOrDefault(song.Track(TrackRole.Bass))?.FirstOrDefault(x => Math.Abs(x.Position - start) < 1e-6) is { Value: { } bass }
                && (bass.Pitches.Min() - tonic).Mod(12) == 7)
                dominant++;
            var run = song.Song.Notes![song.Track(TrackRole.Melody)].Where(x => x.Position >= start - 1e-6 && x.Position < start + song.Map.Ending.Held - 1e-6).ToArray();
            for (var i = 0; i < run.Length; i++)
            {
                var pitch = run[i].Value.Pitches.Max();
                var (chord, _, classes) = Realizer.GetChordNotes(run[i].Value.State);
                notes++;
                if (classes.Contains(pitch.Mod(12))) onChord++;
                else if (Enumerable.Range(0, Core.Composition.Scales.StepCount).Any(x => chord.GetPitch(x).Mod(12) == pitch.Mod(12))) inScale++;
                if (i > 0 && Math.Abs(pitch - run[i - 1].Value.Pitches.Max()) is 1 or 2) byStep++;
            }
        }

        Console.WriteLine($"{songs} cadenzas, held {beats / songs:0.0} beats, on the dominant {dominant / (double)songs:P0}; their runs {notes / (double)songs:0.0} notes, " +
                          $"on the chord {onChord / (double)notes:P0}, of the scale {inScale / (double)notes:P0}, chromatic {(notes - onChord - inScale) / (double)notes:P0}, by step {byStep / (double)notes:P0}");
        await Task.CompletedTask;
    }
}

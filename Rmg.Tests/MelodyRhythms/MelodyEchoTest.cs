using Rmg.Core;
using Rmg.Core.Composition;
using Rmg.Core.Events;

namespace Rmg.Tests.MelodyRhythms;

/// <summary>
///     How the melody plays its notes again: a note of a cycle that repeats the one before, or of a bar that comes back,
///     against the note it echoes, and how the melody moves.
/// </summary>
public sealed class MelodyEchoTest
{
    private const int SongCount = 100;

    /// <summary>
    ///     The melody's echoes, by where their source is: in the same bar, a repeated cycle, or before, a bar that comes
    ///     back; and those over a chord on the same root as their source's, which play the same note, in any octave.
    /// </summary>
    internal sealed record Echoes(int Notes, int InBar, int Later, int SameChord, int SameChordSame, double MeanMove, double LeapShare);

    internal static Echoes Measure(IEnumerable<CorpusSong> songs)
    {
        int notes = 0, inBar = 0, later = 0, sameChord = 0, sameChordSame = 0, leaps = 0, moves = 0;
        double moveSum = 0;
        foreach (var song in songs)
        {
            var heard = new Dictionary<int, (double Position, int Pitch, int Root)>();
            int? previous = null;
            foreach (var note in song.Song.Notes![SongTracks.MelodyTrack])
            {
                var state = note.Value.State;
                var pitch = note.Value.Pitches[0];
                var root = Realizer.GetChord(state).Chord.Root;
                notes++;
                if (previous is { } before)
                {
                    moves++;
                    moveSum += Math.Abs(pitch - before);
                    if (Math.Abs(pitch - before) >= MelodyLine.LeapSize)
                        leaps++;
                }

                previous = pitch;
                var key = state.GetStateValue(StateKinds.Echo);
                if (key == 0)
                    continue;
                if (!heard.TryGetValue(key, out var source))
                {
                    heard[key] = (note.Position, pitch, root);
                    continue;
                }

                if (Math.Floor(source.Position / Meter.BarDuration) == Math.Floor(note.Position / Meter.BarDuration))
                    inBar++;
                else
                    later++;
                if ((source.Root - root).Mod(12) == 0)
                {
                    sameChord++;
                    sameChordSame += (source.Pitch - pitch).Mod(12) == 0 ? 1 : 0;
                }
            }
        }

        return new Echoes(notes, inBar, later, sameChord, sameChordSame, moveSum / moves, leaps / (double)moves);
    }

    [Test]
    [Explicit]
    public async Task Report()
    {
        var echoes = Measure(TestCorpus.Range(SongCount));
        Console.WriteLine($"{echoes.Notes} melody notes; in a repeated cycle {echoes.InBar / (double)echoes.Notes:P0}, " +
                          $"in a bar that comes back {echoes.Later / (double)echoes.Notes:P0}; over the same root as their source " +
                          $"{echoes.SameChord}, the same note {echoes.SameChordSame / (double)echoes.SameChord:P0}; " +
                          $"mean move {echoes.MeanMove:F2} semitones, leaps {echoes.LeapShare:P1}");
        await Task.CompletedTask;
    }

    [Test]
    public async Task Echoes_OverTheSameRoot_MostlyPlayTheSameNote()
    {
        // the others move to a note of the chord on a strong beat, where the chord's shape differs, or end a phrase;
        // before the echoes, the bars that came back played the same note 40% of the time
        var echoes = Measure(TestCorpus.Range(20));

        await Assert.That(echoes.SameChordSame / (double)echoes.SameChord).IsGreaterThan(0.7);
    }
}

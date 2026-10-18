using System.Collections.Immutable;
using Rmg.Core.Composition;
using Rmg.Core.Probabilities;
using Rmg.Core.Songs;

namespace Rmg.Tests.Melodies;

public sealed class HarmonyIntervalTest
{
    [Test]
    public async Task AGenreOfFourthsAndFifths_HarmonisesTheMelodyInThemAlone()
    {
        var genre = new Genre(ImmutableDictionary<string, ImmutableDictionary<string, ByConvention>>.Empty
            .Add("harmonyInterval", ImmutableDictionary<string, ByConvention>.Empty.Add("fourths", new(1, 1, 1)))
            .Add("doubling", ImmutableDictionary<string, ByConvention>.Empty.Add("harmony", new(1, 1, 1))));
        var steps = TestCorpus.InParallel(Enumerable.Range(0, 16), seed => TestCorpus.Get(seed, new SongOverrides(Genre: genre)))
            .SelectMany(song => song.Trace.Where(x => x.Point == TracePoints.LineDoubling).Select(x => ((int, TrackRole Source, TrackRole, int Step))x.Value!))
            .Where(x => x.Source == TrackRole.Melody).Select(x => x.Step).ToArray();

        await Assert.That(steps.Length).IsGreaterThan(0);
        await Assert.That(steps.All(x => x is 3 or -3 or 4 or -4)).IsTrue();
    }

    /// <summary>
    ///     A harmony's weak notes over a sus chord off the third the chord leaves out, moved to the chord's note beside it:
    ///     measured 1.7% of 229 in 64 songs asked to harmonise every section; of the songs' own counter-melody's 5.2% (19.0%
    ///     before), its own line's 7.0%.
    /// </summary>
    [Test]
    public async Task AHarmony_KeepsOffTheThirdASusChordLeavesOut()
    {
        var genre = new Genre(ImmutableDictionary<string, ImmutableDictionary<string, ByConvention>>.Empty
            .Add("doubling", ImmutableDictionary<string, ByConvention>.Empty.Add("harmony", new(1, 1, 1))));
        var (weak, third) = (0, 0);
        foreach (var song in TestCorpus.InParallel(Enumerable.Range(0, 64), seed => TestCorpus.Get(seed, new SongOverrides(Genre: genre))))
        {
            if (!song.Song.Notes!.TryGetValue(song.Track(TrackRole.CounterMelody), out var counter) || !song.Song.Notes.TryGetValue(song.Track(TrackRole.Chords), out var chords))
                continue;
            foreach (var note in counter.Where(x => x.Value.State.GetStateValue(CompositionStateKinds.BeatRank) > 0 && x.Value.State.GetStateValue(CompositionStateKinds.Doubled) > 0))
            {
                // the chord the chords part sounds there, a sus4: its fourth and no third
                var under = chords.LastOrDefault(x => x.Position <= note.Position + 1e-9 && x.Position + x.Value.Duration > note.Position + 1e-9);
                if (under.Value is null)
                    continue;
                var (chord, _, classes) = Realizer.GetChordNotes(under.Value.State);
                var root = Rmg.Core.MathExtensions.Mod(chord.GetPitch(0), 12);
                if (!classes.Contains((root + 5) % 12) || classes.Contains((root + 3) % 12) || classes.Contains((root + 4) % 12))
                    continue;
                weak++;
                third += Rmg.Core.MathExtensions.Mod(note.Value.Pitches[0], 12) == Rmg.Core.MathExtensions.Mod(chord.GetPitch(2), 12) ? 1 : 0;
            }
        }

        Console.WriteLine($"{third} of {weak} harmonised weak notes over a sus4 its third ({third / (double)Math.Max(1, weak):P1})");
        await Assert.That(weak).IsGreaterThan(20);
        await Assert.That(third / (double)weak).IsLessThan(0.12);
    }
}

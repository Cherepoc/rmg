using Rmg.Core;
using Rmg.Core.Composition;
using Rmg.Core.Songs;
using Rmg.Tests.Genres;

namespace Rmg.Tests.SongGenerators;

/// <summary>
///     How a walking bass walks, in the presets whose bass walks in quarters, in the sections it walks in
///     (<see cref="Figures" />' walk): of its notes in a bar of four beats, by
///     the beat each starts on or between the beats, how many are of their chord, how many of its scale outside it, and
///     how many off the scale, chromatic; and how many move by a step (one or two semitones) from the note before, and
///     how many repeat it.
/// </summary>
public sealed class WalkingBassReportTest
{
    private const int SongCount = 64;

    /// <summary>The presets whose bass walks, Jazz's and ska's (<see cref="Figures" />'s walk).</summary>
    private static readonly string[] Genres = ["jazz", "ska"];

    /// <param name="Notes">The bass's notes on a beat or between, by where they start: beats 1 to 4, then between them.</param>
    /// <param name="OnChord">Of those, its chord's notes.</param>
    /// <param name="InScale">Of the scale outside the chord.</param>
    /// <param name="Chromatic">Off the scale.</param>
    /// <param name="ByStep">A step (one or two semitones) from the note before.</param>
    /// <param name="Repeated">The note before's pitch again.</param>
    internal sealed record Counts(int[] Notes, int[] OnChord, int[] InScale, int[] Chromatic, int[] ByStep, int[] Repeated)
    {
        public static Counts Empty() => new(new int[5], new int[5], new int[5], new int[5], new int[5], new int[5]);
    }

    internal static Counts Measure(IEnumerable<CorpusSong> songs)
    {
        var c = Counts.Empty();
        foreach (var song in songs)
        {
            var bass = song.Song.Notes!.GetValueOrDefault(song.Song.Track(TrackRole.Bass))?.ToArray() ?? [];
            // the sections whose bass walks
            var walking = song.Trace.Where(x => x.Point == TracePoints.Figures && ((System.Collections.Immutable.ImmutableDictionary<FigurePlayer, string>)x.Value!).GetValueOrDefault(FigurePlayer.Bass) == "walk")
                .Select(x => x.Section).ToHashSet();
            for (var i = 0; i < bass.Length; i++)
            {
                var note = bass[i];
                if (song.Map.MeterAt(note.Position).BarDuration is not 4 || song.Map.SectionAt(note.Position) is not { } section || !walking.Contains(section.SectionId))
                    continue;
                var beat = song.Map.BeatInBar(note.Position);
                // beats 1 to 4, and between the beats
                var slot = Math.Abs(beat - Math.Round(beat)) < 1e-6 ? (int)Math.Round(beat) % 4 : 4;
                var pitch = note.Value.Pitches.Min();
                var (chord, _, classes) = Realizer.GetChordNotes(note.Value.State);
                var scale = Enumerable.Range(0, Core.Composition.Scales.StepCount).Select(x => chord.GetPitch(x).Mod(12)).ToHashSet();
                c.Notes[slot]++;
                if (classes.Contains(pitch.Mod(12))) c.OnChord[slot]++;
                else if (scale.Contains(pitch.Mod(12))) c.InScale[slot]++;
                else c.Chromatic[slot]++;
                if (i > 0 && Math.Abs(pitch - bass[i - 1].Value.Pitches.Min()) is 1 or 2) c.ByStep[slot]++;
                if (i > 0 && pitch == bass[i - 1].Value.Pitches.Min()) c.Repeated[slot]++;
            }
        }

        return c;
    }

    [Test]
    [Explicit]
    public async Task Report()
    {
        foreach (var genre in Genres)
        {
            var c = Measure(TestCorpus.Streamed(Enumerable.Range(0, SongCount), seed => TestCorpus.Get(seed, new SongOverrides(Genre: GenrePresets.Genre(genre)))));
            string Of(int[] counts, int slot) => c.Notes[slot] == 0 ? "-" : $"{counts[slot] / (double)c.Notes[slot]:P0}";
            var slots = new[] { "beat 1", "beat 2", "beat 3", "beat 4", "between" };
            var weak = c.Notes[1] + c.Notes[3];
            Console.WriteLine($"{genre}: on 2 and 4 off the chord {(c.InScale[1] + c.InScale[3] + c.Chromatic[1] + c.Chromatic[3]) / (double)Math.Max(1, weak):P0} " +
                              $"(chromatic {(c.Chromatic[1] + c.Chromatic[3]) / (double)Math.Max(1, weak):P0}), by step {(c.ByStep[1] + c.ByStep[3]) / (double)Math.Max(1, weak):P0}, repeated {(c.Repeated[1] + c.Repeated[3]) / (double)Math.Max(1, weak):P0}");
            for (var s = 0; s < 5; s++)
                Console.WriteLine($"  {slots[s]}: {c.Notes[s]} notes, on the chord {Of(c.OnChord, s)}, of the scale {Of(c.InScale, s)}, chromatic {Of(c.Chromatic, s)}, by step {Of(c.ByStep, s)}, repeated {Of(c.Repeated, s)}");
        }

        await Task.CompletedTask;
    }
}

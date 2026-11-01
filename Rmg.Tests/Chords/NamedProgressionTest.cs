using System.Collections.Immutable;
using Rmg.Core.Composition;
using Rmg.Core.Songs;
using Rmg.Tests.Genres;

namespace Rmg.Tests.Chords;

/// <summary>
///     How often a genre's sections play its named progression (<see cref="Progressions.Templates" />): of Rock's
///     sections, how many play I–♭VII–IV–I as named, how many play ♭VII–IV–I anywhere in their roots, and how many ♭VII–I,
///     by the roots the trace records (<see cref="TracePoints.Progression" />), and by the section's scale; and the rule that
///     it plays only where its section's scale makes it.
/// </summary>
public sealed class NamedProgressionTest
{
    private const int SongCount = 64;

    /// <summary>
    ///     A named progression plays only where the section's scale makes its chords: asked of every section, Rock's loop's
    ///     ♭VII ten semitones above home and its IV five, never a major scale's vii°, and played in some of them.
    /// </summary>
    [Test]
    public async Task ANamedProgression_PlaysOnlyWhereItsScaleMakesIt()
    {
        var played = 0;
        foreach (var song in TestCorpus.Streamed(Enumerable.Range(0, SongCount), seed => TestCorpus.Get(seed, new SongOverrides(Genre: TestCorpus.Always(GenreChoices.Progression, "rockLoop")))))
        {
            var scales = song.Trace.Where(x => x.Point == TracePoints.SectionScale).ToDictionary(x => x.Section, x => (Scale)x.Value!);
            var homes = song.Trace.Where(x => x.Point == TracePoints.SectionHome).ToDictionary(x => x.Section, x => (int)x.Value!);
            foreach (var entry in song.Trace.Where(x => x.Point == TracePoints.NamedProgression && (string?)x.Value == "rockLoop"))
            {
                played++;
                var (offsets, home) = (scales[entry.Section].Offsets, homes[entry.Section]);
                // a home below the tonic as many steps down, -3 to 3
                int Step(int step) => ((home + step) % 7 + 7) % 7;
                int Above(int step) => ((offsets[Step(step)] - offsets[Step(0)]) % 12 + 12) % 12;
                await Assert.That((Above(6), Above(3))).IsEqualTo((10, 5)).Because($"seed {song.Seed}, section {entry.Section} in {scales[entry.Section].Name} from step {home}");
            }
        }

        await Assert.That(played).IsGreaterThan(SongCount / 4);
    }

    [Test]
    [Explicit]
    public async Task Report()
    {
        var (sections, named, loops, intoHome) = (0, 0, 0, 0);
        var byScale = new Dictionary<string, (int Sections, int Named)>();
        foreach (var song in TestCorpus.Streamed(Enumerable.Range(0, SongCount), seed => TestCorpus.Get(seed, new SongOverrides(Genre: GenrePresets.Genre("rock")))))
        {
            var scales = song.Trace.Where(x => x.Point == TracePoints.SectionScale).ToDictionary(x => x.Section, x => ((Scale)x.Value!).Name);
            foreach (var entry in song.Trace.Where(x => x.Point == TracePoints.Progression))
            {
                int[] roots = [..((ImmutableArray<int>)entry.Value!).Select(x => (x % 7 + 7) % 7)];
                sections++;
                var isNamed = song.Trace.Any(x => x.Point == TracePoints.NamedProgression && x.Section == entry.Section && (string?)x.Value == "rockLoop");
                named += isNamed ? 1 : 0;
                loops += Enumerable.Range(0, roots.Length).Any(i => roots[i] == 6 && roots[(i + 1) % roots.Length] == 3 && roots[(i + 2) % roots.Length] == 0) ? 1 : 0;
                intoHome += Enumerable.Range(0, roots.Length).Any(i => roots[i] == 6 && roots[(i + 1) % roots.Length] == 0) ? 1 : 0;
                var scale = scales[entry.Section];
                var s = byScale.GetValueOrDefault(scale);
                byScale[scale] = (s.Sections + 1, s.Named + (isNamed ? 1 : 0));
            }
        }

        Console.WriteLine($"rock: {sections} sections, I–♭VII–IV–I as named {named / (double)sections:P0}, ♭VII–IV–I {loops / (double)sections:P0}, ♭VII–I {intoHome / (double)sections:P0}");
        foreach (var (scale, s) in byScale.OrderByDescending(x => x.Value.Sections))
            Console.WriteLine($"  {scale}: {s.Sections} sections, named {s.Named / (double)s.Sections:P0}");
        await Task.CompletedTask;
    }
}

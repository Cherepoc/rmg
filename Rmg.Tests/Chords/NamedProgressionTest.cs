using System.Collections.Immutable;
using Rmg.Core.Composition;
using Rmg.Core.Songs;
using Rmg.Tests.Genres;

namespace Rmg.Tests.Chords;

/// <summary>
///     How often a genre's sections play its named progressions (<see cref="Core.Composition.Progressions.Templates" />): of each genre's
///     sections, how many play each of its templates as named, and how many play the template's roots anywhere, drawn or
///     named, by the roots the trace records (<see cref="TracePoints.Progression" />), and by the section's scale; and the
///     rule that a template plays only where its section's scale makes it.
/// </summary>
public sealed class NamedProgressionTest
{
    private const int SongCount = 64;

    /// <summary>The genres that name a template, and the templates each names.</summary>
    private static readonly (string Genre, string[] Templates)[] Named =
    [
        ("rock", ["rockLoop"]), ("pop", ["axis", "minorAxis"]), ("ballad", ["axis", "minorAxis"]), ("punk", ["axis"]), ("trance", ["minorAxis"]),
        ("synthwave", ["minorAxis"]), ("surf", ["andalusian"]), ("latin", ["andalusian"]), ("metal", ["andalusian"]), ("dungeon", ["andalusian"])
    ];

    /// <summary>
    ///     A named progression plays only where the section's scale makes its chords: asked of every section, each template's
    ///     roots their semitones above home, every chord major or minor as its scale makes it, a major fifth's raised so in
    ///     every chord on it; and each played in some of them.
    /// </summary>
    [Test]
    [MethodDataSource(nameof(TemplateNames))]
    public async Task ANamedProgression_PlaysOnlyWhereItsScaleMakesIt(string name)
    {
        var template = Core.Composition.Progressions.Templates.Single(x => x.Name == name);
        var played = 0;
        foreach (var song in TestCorpus.Streamed(Enumerable.Range(0, SongCount), seed => TestCorpus.Get(seed, new SongOverrides(Genre: TestCorpus.Always(GenreChoices.Progression, name)))))
        {
            var scales = song.Trace.Where(x => x.Point == TracePoints.SectionScale).ToDictionary(x => x.Section, x => (Scale)x.Value!);
            var homes = song.Trace.Where(x => x.Point == TracePoints.SectionHome).ToDictionary(x => x.Section, x => (int)x.Value!);
            var progressions = song.Trace.Where(x => x.Point == TracePoints.Progression).ToDictionary(x => x.Section, x => (ImmutableArray<int>)x.Value!);
            var chordScales = song.Trace.Where(x => x.Point == TracePoints.ChordScales).ToDictionary(x => x.Section, x => (ImmutableArray<ChordScale>)x.Value!);
            foreach (var entry in song.Trace.Where(x => x.Point == TracePoints.NamedProgression && (string?)x.Value == name))
            {
                played++;
                var (offsets, home) = (scales[entry.Section].Offsets, homes[entry.Section]);
                // a home below the tonic as many steps down, -3 to 3
                int Step(int step) => ((home + step) % 7 + 7) % 7;
                int Above(int from, int step) => ((offsets[Step(step)] - offsets[Step(from)]) % 12 + 12) % 12;
                var because = $"seed {song.Seed}, section {entry.Section} in {scales[entry.Section].Name} from step {home}";
                await Assert.That(template.Steps.Select(x => Above(0, x)).ToArray()).IsEquivalentTo(template.Semitones.ToArray()).Because(because);
                foreach (var step in template.Steps.Where(x => !(template.MajorFifth && x == 4)))
                    await Assert.That((Above(step, step + 2), Above(step, step + 4))).IsIn((4, 7), (3, 7)).Because($"{because}, its chord on {step}");
                if (template.MajorFifth)
                {
                    // raised, or a passing chord's dominant, major as well
                    var fifths = progressions[entry.Section].Select((root, i) => (root, i)).Where(x => (x.root % 7 + 7) % 7 == 4).ToArray();
                    await Assert.That(fifths.All(x => chordScales[entry.Section][x.i].Kind is ChordScaleKind.Cadence or ChordScaleKind.Applied)).IsTrue().Because($"{because}, its fifths raised");
                }
            }
        }

        await Assert.That(played).IsGreaterThan(SongCount / 4).Because(name);
    }

    public static IEnumerable<string> TemplateNames() => Core.Composition.Progressions.Templates.Select(x => x.Name);

    [Test]
    [Explicit]
    public async Task Report()
    {
        foreach (var (genre, names) in Named)
        {
            var sections = 0;
            var named = names.ToDictionary(x => x, _ => 0);
            var heard = names.ToDictionary(x => x, _ => 0);
            var byScale = new Dictionary<string, (int Sections, int Named)>();
            foreach (var song in TestCorpus.Streamed(Enumerable.Range(0, SongCount), seed => TestCorpus.Get(seed, new SongOverrides(Genre: GenrePresets.Genre(genre)))))
            {
                var scales = song.Trace.Where(x => x.Point == TracePoints.SectionScale).ToDictionary(x => x.Section, x => ((Scale)x.Value!).Name);
                var plays = song.Trace.Where(x => x.Point == TracePoints.NamedProgression).ToDictionary(x => x.Section, x => (string?)x.Value);
                foreach (var entry in song.Trace.Where(x => x.Point == TracePoints.Progression))
                {
                    int[] roots = [..((ImmutableArray<int>)entry.Value!).Select(x => (x % 7 + 7) % 7)];
                    sections++;
                    var playing = plays.GetValueOrDefault(entry.Section);
                    foreach (var name in names)
                    {
                        var steps = Core.Composition.Progressions.Templates.Single(x => x.Name == name).Steps.Distinct().ToArray();
                        named[name] += playing == name ? 1 : 0;
                        // its distinct roots in its order, from anywhere in the section's, round the end
                        heard[name] += Enumerable.Range(0, roots.Length).Any(i => steps.Select((s, k) => roots[(i + k) % roots.Length] == s).All(x => x)) ? 1 : 0;
                    }

                    var scale = scales[entry.Section];
                    var s = byScale.GetValueOrDefault(scale);
                    byScale[scale] = (s.Sections + 1, s.Named + (playing is not null ? 1 : 0));
                }
            }

            Console.WriteLine($"{genre}: {sections} sections; " + string.Join("; ", names.Select(x => $"{x} as named {named[x] / (double)sections:P0}, its roots heard {heard[x] / (double)sections:P0}")));
            foreach (var (scale, s) in byScale.OrderByDescending(x => x.Value.Sections))
                Console.WriteLine($"  {scale}: {s.Sections} sections, named {s.Named / (double)s.Sections:P0}");
        }

        await Task.CompletedTask;
    }
}

using System.Collections.Immutable;
using Rmg.Core.Composition;
using Rmg.Core.Songs;

namespace Rmg.Tests.Genres;

/// <summary>
///     Rock and metal against their sound: a riff-driven hard rock band's progressions, a blues-rock band's shuffle, a
///     solo in most songs, a thrash band's double kick, a heavy band's riffs of two and four bars; beside the songs with no
///     genre.
/// </summary>
public sealed class RockAndMetalReportTest
{
    private const int Count = 64;

    /// <summary>Whether a progression plays the roots given, in steps above its home, one after another, round its end too.</summary>
    private static bool Plays(ImmutableArray<int> progression, int[] roots)
    {
        var steps = progression.Select(x => (x % 7 + 7) % 7).ToArray();
        return Enumerable.Range(0, steps.Length).Any(i => roots.Select((r, k) => steps[(i + k) % steps.Length] == r).All(x => x));
    }

    [Test]
    [Explicit]
    public async Task Report()
    {
        foreach (var (name, genre) in new (string, Genre?)[] { ("rock", GenrePresets.Genre("rock")), ("metal", GenrePresets.Genre("metal")), ("nu metal", GenrePresets.Genre("nuMetal")), ("no genre", null) })
        {
            var songs = TestCorpus.InParallel(Enumerable.Range(0, Count), seed => TestCorpus.Get(seed, new SongOverrides(Genre: genre)));
            var progressions = songs.SelectMany(x => x.Trace.Where(t => t.Point == TracePoints.Progression).Select(t => (ImmutableArray<int>)t.Value!)).ToArray();
            double Share(int[] roots) => progressions.Average(x => Plays(x, roots) ? 1.0 : 0);
            var schemes = songs.SelectMany(x => x.Trace.Where(t => t.Point == TracePoints.PhraseScheme).Select(t => ((PhraseScheme)t.Value!).ToString().Replace("′", ""))).ToArray();
            string Scheme(string letters) => $"{letters} {schemes.Count(x => x == letters) / (double)schemes.Length:P0}";

            // the kick's hits a bar in a song's loudest section, and in all
            var kicks = DrumGroups.Kick.Drums.Select(DrumGroups.GetTrackNumber).ToHashSet();
            var (loudest, every) = (new List<double>(), new List<double>());
            // a song whose chords play a clean guitar in its quietest section and a driven one in its loudest
            var cleanThenDriven = 0;
            foreach (var song in songs)
            {
                var energies = song.Trace.Where(t => t.Point == TracePoints.SectionEnergy).GroupBy(t => t.Section).ToDictionary(g => g.Key, g => ((SectionEnergyTrace)g.First().Value!).Energy);
                var hits = song.Song.Notes!.Where(x => kicks.Contains(x.Key)).SelectMany(x => x.Value).Select(x => x.Position).ToArray();
                var bars = song.Map.Sections.Select(x => (x.SectionId, Hits: hits.Count(p => p >= x.Start - 1e-9 && p < x.End - 1e-9) / (x.Duration / x.Meter.BarDuration))).ToArray();
                if (bars.Length == 0)
                    continue;
                every.AddRange(bars.Select(x => x.Hits));
                loudest.Add(bars.MaxBy(x => energies.GetValueOrDefault(x.SectionId))!.Hits);

                if (song.Song.Notes!.GetValueOrDefault(SongTracks.ChordsTrack) is { } chords && song.Song.TrackDefinitions[SongTracks.ChordsTrack] is PitchInstrumentTrack own)
                {
                    int[] Programs(SectionSpan span) => [..chords.Where(x => x.Position >= span.Start - 1e-9 && x.Position < span.End - 1e-9)
                        .Select(x => x.Value.State.GetStateValue(CompositionStateKinds.Program) is var stated and > 0 ? stated - 1 : own.InstrumentCode)];
                    var quiet = Programs(song.Map.Sections.MinBy(x => energies.GetValueOrDefault(x.SectionId))!);
                    var loud = Programs(song.Map.Sections.MaxBy(x => energies.GetValueOrDefault(x.SectionId))!);
                    if (quiet.Any(x => x is >= 24 and <= 28) && loud.Any(x => x is 29 or 30))
                        cleanThenDriven++;
                }
            }

            var solos = songs.Where(x => x.Solos.Count > 0).ToArray();
            var organs = solos.Count(x => SoloProgram(x) is >= 16 and <= 20);
            static int SoloProgram(CorpusSong x) => x.Song.TrackDefinitions.TryGetValue(SongTracks.SoloTrack, out var solo) && solo is PitchInstrumentTrack pitched ? pitched.InstrumentCode : -1;
            var structures = songs.Select(x => (SongStructure)x.Trace.Single(t => t.Point == TracePoints.SongForm).Value!).ToArray();

            Console.WriteLine($"{name}: songs with a solo {solos.Length / (double)songs.Length:P0}, an organ soloing {organs / (double)Math.Max(1, solos.Length):P0} of them; " +
                              $"swung {songs.Average(x => x.Swing.Delay > 0 ? 1.0 : 0):P0}");
            // the chords drawn, every one but a progression's first, home, on each degree
            var drawn = progressions.SelectMany(x => x.Skip(1).Select(r => (r % 7 + 7) % 7)).ToArray();
            string Degree(int step) => $"{drawn.Count(x => x == step) / (double)drawn.Length:P0}";
            Console.WriteLine($"{name}: sections playing ♭VII–IV–I {Share([6, 3, 0]):P0}, ♭VI–♭VII–I {Share([5, 6, 0]):P0}, i–♭VI–♭VII {Share([0, 5, 6]):P0}, " +
                              $"♭VII–I {Share([6, 0]):P0}; the chords drawn on IV {Degree(3)}, ♭VI {Degree(5)}, ♭VII {Degree(6)}");
            Console.WriteLine($"{name}: schemes {Scheme("AAAA")}, {Scheme("AAAB")}, {Scheme("ABAB")}, {Scheme("AABA")}, {Scheme("AABB")}, others {schemes.Count(x => x is not ("AAAA" or "AAAB" or "ABAB" or "AABA" or "AABB")) / (double)schemes.Length:P0}");
            var figures = songs.SelectMany(x => x.Trace.Where(t => t.Point == TracePoints.Figures).Select(t => (ImmutableDictionary<FigurePlayer, string>)t.Value!)).ToArray();
            string Figure(string figure) => $"{figure} {figures.Count(x => x.Values.Contains(figure)) / (double)Math.Max(1, figures.Length):P0}";
            Console.WriteLine($"{name}: the kick's hits a bar {every.Average():0.0}, in the loudest section {loudest.Average():0.0}, at most {loudest.DefaultIfEmpty(0).Max():0.0}; " +
                              $"sections playing {Figure("withRiff")}, {Figure("doubleKick")}, {Figure("gallop")}");
            Console.WriteLine($"{name}: songs opening on an intro section {structures.Average(x => x.Roles.Values.Contains(SectionRole.Intro) ? 1.0 : 0):P0}, " +
                              $"its chords clean in its quietest section and driven in its loudest {cleanThenDriven / (double)songs.Count():P0}");
        }

        await Task.CompletedTask;
    }
}

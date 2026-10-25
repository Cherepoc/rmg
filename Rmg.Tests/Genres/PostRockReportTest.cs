using Rmg.Core.Composition;
using Rmg.Core.Songs;

namespace Rmg.Tests.Genres;

/// <summary>Post-rock's songs as a whole: their length, their build from quiet to loud and back, and where their tremolo and their driven guitars fall.</summary>
public sealed class PostRockReportTest
{
    /// <summary>
    ///     Over 64 Post-rock songs: their length in minutes; each third of a song's loudness, its notes' mean velocity, and its
    ///     density, its notes a second; the songs whose last third or middle is the loudest and whose first is the quietest;
    ///     the share of a song's tremolo-picked notes in its louder half of sections; the driven guitars' share of the
    ///     guitars' notes in its quieter and its louder half; and the songs' endings (measured over 64 songs on 26 October
    ///     2026: 8.5 minutes the median, 5.9 to 11.2; the thirds' loudness 0.04, 0.30 and 0.46, their notes 9.5, 13.8 and
    ///     17.9 a second; 62 songs loudest after their start, 56 quietest at it; 18131 of 27564 picked notes in the louder half;
    ///     the guitars driven in 44% of the quieter half's notes and 45% of the louder's, a section heavy every time it plays
    ///     however quiet its first appearance; 43 songs ringing out, 21 fading).
    /// </summary>
    [Test]
    [Explicit]
    public async Task Report()
    {
        var songs = TestCorpus.InParallel(Enumerable.Range(0, 64), seed => TestCorpus.Get(seed, new SongOverrides(Genre: GenrePresets.Genre("postRock")))).ToArray();
        var thirds = new double[3][];
        for (var k = 0; k < 3; k++)
            thirds[k] = new double[2];
        var (peakLate, quietStart, pickedLoud, picked, drivenQuiet, guitarsQuiet, drivenLoud, guitarsLoud) = (0, 0, 0, 0, 0, 0, 0, 0);
        var minutes = new List<double>();
        foreach (var song in songs)
        {
            minutes.Add(song.Rendered.Seconds / 60);
            var notes = song.Song.Notes!.SelectMany(x => x.Value).ToArray();
            var end = song.Map.Sections[^1].End;
            double[] Third(int k) => [..notes.Where(x => x.Position >= k * end / 3 && x.Position < (k + 1) * end / 3).Select(x => x.Value.Velocity)];
            var loud = Enumerable.Range(0, 3).Select(k => Third(k)).Select(x => x.DefaultIfEmpty().Average()).ToArray();
            for (var k = 0; k < 3; k++)
            {
                thirds[k][0] += loud[k] / songs.Length;
                thirds[k][1] += Third(k).Length / Math.Max(1e-9, song.Rendered.SecondsAt(end) / 3) / songs.Length;
            }
            peakLate += loud[0] < loud[1] || loud[0] < loud[2] ? loud.ToList().IndexOf(loud.Max()) > 0 ? 1 : 0 : 0;
            quietStart += loud[0] == loud.Min() ? 1 : 0;
            // the sections by their loudness, the louder half's
            var sections = song.Map.Sections.Select((x, i) => (i, Loudness: notes.Where(n => n.IsWithin(x.Start, x.End)).Select(n => n.Value.Velocity).DefaultIfEmpty().Average())).ToArray();
            var median = sections.Select(x => x.Loudness).Order().ElementAt(sections.Length / 2);
            bool Loud(double position) => song.Map.PlayingAt(position) is { Index: var i } && sections[i].Loudness >= median;
            foreach (var note in notes.Where(x => x.Value.State.GetStateValue(CompositionStateKinds.Picked) == 1))
                (picked, pickedLoud) = (picked + 1, pickedLoud + (Loud(note.Position) ? 1 : 0));
            foreach (var (track, timeline) in song.Song.Notes!)
                if (song.Song.TrackDefinitions[track] is PitchInstrumentTrack part)
                    foreach (var note in timeline)
                    {
                        var program = InstrumentsOverTime.ProgramOf(note.Value.State, part.InstrumentCode);
                        if (!InstrumentTraits.IsGuitar(program))
                            continue;
                        var driven = InstrumentTraits.Has(program, InstrumentTrait.Driven) ? 1 : 0;
                        if (Loud(note.Position))
                            (guitarsLoud, drivenLoud) = (guitarsLoud + 1, drivenLoud + driven);
                        else
                            (guitarsQuiet, drivenQuiet) = (guitarsQuiet + 1, drivenQuiet + driven);
                    }
        }

        minutes.Sort();
        Console.WriteLine($"length: median {minutes[minutes.Count / 2]:F1} min, from {minutes[0]:F1} to {minutes[^1]:F1}");
        Console.WriteLine($"thirds, loudness and notes a second: {string.Join(" | ", thirds.Select(x => $"{x[0]:F2} {x[1]:F1}"))}");
        Console.WriteLine($"songs loudest later than their start {peakLate}, quietest at their start {quietStart}, of {songs.Length}");
        Console.WriteLine($"tremolo-picked notes in the louder half of the sections {pickedLoud} of {picked}");
        Console.WriteLine($"driven guitars' share: quieter half {drivenQuiet / (double)Math.Max(1, guitarsQuiet):P0}, louder half {drivenLoud / (double)Math.Max(1, guitarsLoud):P0}");
        Console.WriteLine($"endings: {string.Join(", ", songs.GroupBy(x => x.Map.Ending.Kind).OrderByDescending(x => x.Count()).Select(x => $"{x.Key} {x.Count()}"))}");
        await Task.CompletedTask;
    }
}

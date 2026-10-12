using System.Collections.Immutable;
using Rmg.Core;
using Rmg.Core.Composition;
using Rmg.Core.Events;
using Rmg.Core.Songs;

namespace Rmg.Tests.Genres;

/// <summary>
///     The metal family against its sound: how long its chords hold still, how low its riff plays and whether its tonic is
///     the guitar's floor, how many pitches a bar its riff plays, and how closely its kick and bass play with it.
/// </summary>
public sealed class MetalFamilyReportTest
{
    private const int Count = 64;

    private static readonly string[] Genres = ["nuMetal", "djent", "metal", "thrash", "symphonic", "doom", "blackMetal", "rock", "techno", "funk"];

    [Test]
    [Explicit]
    public async Task Report()
    {
        foreach (var name in Genres)
        {
            var songs = TestCorpus.InParallel(Enumerable.Range(0, Count), seed => TestCorpus.Get(seed, new SongOverrides(Genre: GenrePresets.Genre(name))));
            var spans = songs.SelectMany(x => x.Trace.Where(t => t.Point == TracePoints.HarmonicRhythm).Select(t => ((HarmonicRhythm)t.Value!).Bars)).ToArray();
            string Span(double bars) => $"{spans.Count(x => x == bars) / (double)spans.Length:P0}";

            // the longest a root holds in a section, in bars, and the share of the sections' bars under a root held four bars or more
            var (longest, stillBars, allBars) = (new List<double>(), 0.0, 0.0);
            // the riff's lowest note, whether it is the key's tonic, and its pitch classes a bar
            var (lowest, tonicLowest, classes) = (new List<int>(), 0, new List<double>());
            // a riff's onsets with the bass on them, and its sections with the kick's figure its own
            var (riffOnsets, withBass) = (0, 0);
            foreach (var song in songs)
            {
                var common = song.Song.TrackEventStateTimelineMap.CommonStateTimelineMap;
                var roots = common.GetStateTimeline(StateKinds.ChordRoot).Select(x => (x.Position, x.Value)).ToArray();
                foreach (var section in song.Map.Sections)
                {
                    var bar = section.Meter.BarDuration;
                    // the root sounding as the section starts, a vamp's the only one
                    (double Position, int Value)[] inside = [(section.Start, common.GetEffectiveStateMapAt(section.Start).GetStateValue(StateKinds.ChordRoot)),
                        ..roots.Where(x => x.Position > section.Start + 1e-9 && x.Position < section.End - 1e-9)];
                    var runs = new List<double>();
                    for (var i = 0; i < inside.Length;)
                    {
                        var j = i;
                        while (j + 1 < inside.Length && inside[j + 1].Value == inside[i].Value)
                            j++;
                        var end = j + 1 < inside.Length ? inside[j + 1].Position : section.End;
                        runs.Add((end - inside[i].Position) / bar);
                        i = j + 1;
                    }

                    if (runs.Count == 0)
                        continue;
                    longest.Add(runs.Max());
                    stillBars += runs.Where(x => x >= 4 - 1e-9).Sum();
                    allBars += runs.Sum();
                }

                if (song.Absent.Contains(TrackRole.Riff))
                    continue;
                var riff = song.Notes(song.Track(TrackRole.Riff));
                if (riff.Length == 0)
                    continue;
                var lowestNote = riff.MinBy(x => x.Value.Offset);
                var low = lowestNote.Value.Offset;
                var key = song.Song.TrackEventStateTimelineMap.CommonStateTimelineMap.GetEffectiveStateMapAt(lowestNote.Position).GetStateValue(StateKinds.KeyOffset);
                lowest.Add(low);
                if (((low - key) % 12 + 12) % 12 == 0)
                    tonicLowest++;
                classes.AddRange(riff.GroupBy(x => (int)Math.Floor(x.Position / song.Song.Meter.BarDuration)).Select(g => (double)g.Select(x => x.Value.Offset % 12).Distinct().Count()));
                if (!song.Absent.Contains(TrackRole.Bass))
                {
                    var bass = song.Notes(song.Track(TrackRole.Bass)).Select(x => Math.Round(x.Position * 48)).ToHashSet();
                    riffOnsets += riff.Length;
                    withBass += riff.Count(x => bass.Contains(Math.Round(x.Position * 48)));
                }
            }

            var doubled = songs.Sum(x => x.Trace.Count(t => t.Point == TracePoints.LineDoubling && ((int, TrackRole, TrackRole, int))t.Value! is (_, TrackRole.Riff, TrackRole.Bass, _)));
            var withRiff = songs.Where(x => !x.Absent.Contains(TrackRole.Riff) && !x.Absent.Contains(TrackRole.Bass)).ToArray();
            var sections = withRiff.Sum(x => x.Map.Sections.Length);
            // the songs' bass a slap bass, General MIDI's 36 and 37
            var slapped = withRiff.Count(x => x.Song.TrackDefinitions[x.Track(TrackRole.Bass)] is PitchInstrumentTrack { InstrumentCode: 36 or 37 });
            Console.WriteLine($"{name}: the bass doubling the riff in {doubled / (double)Math.Max(1, sections):P0} of the sections both play, a slap bass in {slapped / (double)Math.Max(1, withRiff.Length):P0} of the songs");
            // the riff's lowest notes over their chord's root, on its beats and off them
            var intervals = songs.Where(x => !x.Absent.Contains(TrackRole.Riff)).SelectMany(x => x.Song.Notes!.GetValueOrDefault(x.Track(TrackRole.Riff))?.Select(n => n.Value) ?? [])
                .Select(n => (Beat: n.State.GetStateValue(CompositionStateKinds.BeatRank) == 0, Chugs: n.State.GetStateValue(CompositionStateKinds.LinePedal) > 0, Interval: (n.Pitches.Min() - Realizer.GetChord(n.State).Chord.GetPitch(0)).Mod(12))).ToArray();
            string Intervals(bool beat, bool chugs = false)
            {
                var these = intervals.Where(x => x.Beat == beat && (!chugs || x.Chugs)).Select(x => x.Interval).ToArray();
                string Of(string label, params int[] classes) => $"{label} {these.Count(classes.Contains) / (double)Math.Max(1, these.Length):P0}";
                return string.Join(", ", Of("root", 0), Of("♭2", 1), Of("♭3", 3), Of("4", 5), Of("♭5", 6), Of("5", 7), Of("♭7", 10), Of("other", 2, 4, 8, 9, 11));
            }

            Console.WriteLine($"{name}: the riff over its root on the beats: {Intervals(true)}");
            Console.WriteLine($"{name}: the riff over its root off them: {Intervals(false)}");
            Console.WriteLine($"{name}: the riff over its root off them where it chugs, {intervals.Count(x => x.Chugs) / (double)Math.Max(1, intervals.Length):P0} of its notes: {Intervals(false, true)}");
            var scales = songs.SelectMany(x => x.Trace.Where(t => t.Point == TracePoints.LineScale && !x.Absent.Contains(TrackRole.Riff) && t.Track == x.Track(TrackRole.Riff)).Select(t => (LineScale)t.Value!)).ToArray();
            Console.WriteLine($"{name}: the riff's scales {string.Join(", ", scales.GroupBy(x => x).OrderBy(x => x.Key).Select(g => $"{g.Key} {g.Count() / (double)scales.Length:P0}"))}");
            var figures = songs.SelectMany(x => x.Trace.Where(t => t.Point == TracePoints.Figures).Select(t => (ImmutableDictionary<FigurePlayer, string>)t.Value!)).ToArray();
            Console.WriteLine($"{name}: {longest.Count} sections; spans half {Span(0.5)}, one {Span(1)}, two {Span(2)}; a section's longest root {longest.Average():0.0} bars, " +
                              $"its bars under a root of four or more {stillBars / allBars:P0}");
            Console.WriteLine($"{name}: the riff's lowest {lowest.DefaultIfEmpty().Average():0.0} (min {lowest.DefaultIfEmpty().Min()}), the tonic {tonicLowest / (double)Math.Max(1, lowest.Count):P0}, " +
                              $"pitch classes a bar {classes.DefaultIfEmpty().Average():0.00}; its onsets with the bass {withBass / (double)Math.Max(1, riffOnsets):P0}, " +
                              $"sections with the kick on it {figures.Count(x => x.GetValueOrDefault(FigurePlayer.Ground) == "withRiff") / (double)Math.Max(1, figures.Length):P0}");
        }

        await Task.CompletedTask;
    }
}

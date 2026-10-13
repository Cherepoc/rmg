using Rmg.Core.Composition;
using Rmg.Core.Songs;
using Rmg.Tests.Genres;

namespace Rmg.Tests.Melodies;

/// <summary>How a melody would sing: its rests, its notes a bar, its repeated notes and leaps, how legato and how syncopated.</summary>
public sealed class SungMelodyReportTest
{
    [Test]
    [Explicit]
    [Arguments(null)]
    [Arguments("blues")]
    [Arguments("folk")]
    [Arguments("pop")]
    [Arguments("hipHop")]
    [Arguments("techno")]
    public async Task Report(string? genre)
    {
        var songs = TestCorpus.InParallel(Enumerable.Range(0, 128), seed => genre is null ? TestCorpus.Get(seed) : TestCorpus.Get(seed, new SongOverrides(Genre: GenrePresets.Genre(genre))))
            .Where(x => !x.Absent.Contains(TrackRole.Melody)).ToArray();
        var report = new System.Text.StringBuilder(genre ?? "the songs' own").AppendLine();
        var (bars, notes, busyBars, runs, longRuns, veryLong, intervals, repeats, steps, thirds, fourthsToSixths, leaps) = (0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0);
        var (legato, legatoCount, offEighth, onDownbeat, runBeats) = (0.0, 0, 0, 0, 0.0);
        var notesPerRun = new List<int>();
        var leapSizes = new int[25];
        var together = 0;
        var gaps = new int[4];
        var (lastEnds, lastCount, innerEnds, innerCount, halfRests, halfCount) = (0, 0, 0, 0, 0, 0);
        var durations = new List<double>();
        foreach (var song in songs)
        foreach (var span in song.Map.Sections.Where(s => !song.IsSolo(s)))
        {
            var n = song.Notes(song.Track(TrackRole.Melody)).Where(x => x.Position >= span.Start - 1e-9 && x.Position < span.End - 1e-9)
                // the sung line alone, the top note where the part plays octaves or a chord
                .GroupBy(x => Math.Round(x.Position, 6)).Select(g => g.MaxBy(x => x.Value.Offset)).ToArray();
            if (n.Length < 2)
                continue;
            var bar = span.Meter.BarDuration;
            // the gaps between the melody's onsets as written, before swing: 16ths or quicker, a triplet's 8ths, 8ths, longer
            var written = (song.Song.Notes!.GetValueOrDefault(song.Track(TrackRole.Melody))?.Select(x => x.Position) ?? [])
                .Where(x => x >= span.Start - 1e-9 && x < span.End - 1e-9).Distinct().Order().ToArray();
            foreach (var gap in written.Zip(written.Skip(1)).Select(x => x.Second - x.First))
                gaps[gap < 0.3 ? 0 : gap < 0.4 ? 1 : gap < 0.6 ? 2 : 3]++;
            // a rest, a gap of half a beat or more, in each phrase's last bar, the pattern's last phrase and the ones within it
            for (var end = span.Start + span.Meter.PhraseDuration; end <= span.End + 1e-9; end += span.Meter.PhraseDuration)
            {
                var rests = n.Where((x, i) => x.Position >= end - bar - 1e-9 && x.Position < end - 1e-9
                                                 && (i + 1 == n.Length ? end : n[i + 1].Position) - (x.Position + x.Value.Duration) >= 0.5 - 1e-6).Any()
                               || !n.Any(x => x.Position >= end - bar - 1e-9 && x.Position < end - 1e-9);
                var half = end - span.Meter.PhraseDuration / 2;
                if (span.Meter.PhraseBars >= 4)
                    (halfRests, halfCount) = (halfRests + (n.Where((x, i) => x.Position >= half - bar - 1e-9 && x.Position < half - 1e-9
                                                                            && (i + 1 == n.Length ? half : n[i + 1].Position) - (x.Position + x.Value.Duration) >= 0.5 - 1e-6).Any() ? 1 : 0), halfCount + 1);
                var isLast = Math.Abs((end - span.Start) % span.Meter.PatternDuration) < 1e-6;
                if (isLast) (lastEnds, lastCount) = (lastEnds + (rests ? 1 : 0), lastCount + 1);
                else (innerEnds, innerCount) = (innerEnds + (rests ? 1 : 0), innerCount + 1);
            }
            bars += span.BarCount;
            notes += n.Length;
            busyBars += n.GroupBy(x => (int)Math.Floor((x.Position - span.Start) / bar + 1e-9)).Count(g => g.Count() >= 8);
            var runStart = 0;
            for (var i = 0; i < n.Length; i++)
            {
                var pos = n[i].Position - span.Start;
                offEighth += Math.Abs(pos * 2 - Math.Round(pos * 2)) > 1e-6 ? 1 : 0;
                durations.Add(n[i].Value.Duration);
                var endsRun = i + 1 == n.Length || n[i + 1].Position - (n[i].Position + n[i].Value.Duration) >= 0.5 - 1e-6;
                if (i + 1 < n.Length)
                {
                    var ioi = n[i + 1].Position - n[i].Position;
                    if (ioi > 1e-6 && ioi <= 2)
                        (legato, legatoCount) = (legato + Math.Min(1, n[i].Value.Duration / ioi), legatoCount + 1);
                    var interval = Math.Abs(n[i + 1].Value.Offset - n[i].Value.Offset);
                    together += ioi < 1e-6 ? 1 : 0;
                    if (!endsRun && interval > 9) leapSizes[Math.Min(24, interval)]++;
                    if (!endsRun)
                    {
                        intervals++;
                        if (interval == 0) repeats++;
                        else if (interval <= 2) steps++;
                        else if (interval <= 4) thirds++;
                        else if (interval <= 9) fourthsToSixths++;
                        else leaps++;
                    }
                }

                if (endsRun)
                {
                    var length = n[i].Position + n[i].Value.Duration - n[runStart].Position;
                    runs++;
                    runBeats += length;
                    longRuns += length > 2 * bar + 1e-6 ? 1 : 0;
                    veryLong += length > 4 * bar + 1e-6 ? 1 : 0;
                    var start = n[runStart].Position - span.Start;
                    onDownbeat += Math.Abs(start / bar - Math.Round(start / bar)) < 1e-6 ? 1 : 0;
                    notesPerRun.Add(i - runStart + 1);
                    runStart = i + 1;
                }
            }
        }

        notesPerRun.Sort();
        durations.Sort();
        report.AppendLine($"{songs.Length} songs, {bars} bars, {notes / (double)bars:0.0} notes a bar, {busyBars / (double)bars:P0} of bars of 8 notes or more");
        report.AppendLine($"rests (a gap of half a beat or more): a phrase {runBeats / runs:0.0} beats, {notesPerRun.Average():0.0} notes (median {notesPerRun[notesPerRun.Count / 2]}), " +
                          $"{longRuns / (double)runs:P0} over two bars, {veryLong / (double)runs:P0} over four; {onDownbeat / (double)runs:P0} start on a downbeat");
        report.AppendLine($"moves in a phrase: repeated {repeats / (double)intervals:P0}, steps {steps / (double)intervals:P0}, thirds {thirds / (double)intervals:P0}, " +
                          $"4ths-6ths {fourthsToSixths / (double)intervals:P0}, over a 6th {leaps / (double)intervals:P0}");
        report.AppendLine($"legato (duration over the gap to the next) {legato / legatoCount:P0}; off the 8ths {offEighth / (double)notes:P0}; " +
                          $"durations: median {durations[durations.Count / 2]:0.00}, 90th {durations[durations.Count * 9 / 10]:0.00} beats");
        report.AppendLine($"onsets together {together}; leaps over a 6th by size: {string.Join(", ", leapSizes.Select((c, i) => (c, i)).Where(x => x.c > 0).Select(x => $"{x.i}:{x.c}"))}");
        report.AppendLine($"a rest in a phrase's last bar: the pattern's last phrase {lastEnds / (double)lastCount:P0} of {lastCount}, a phrase within it {innerEnds / (double)Math.Max(1, innerCount):P0} of {innerCount}; at half a phrase {halfRests / (double)Math.Max(1, halfCount):P0} of {halfCount}");
        report.AppendLine($"onsets apart, as written: a 16th or less {gaps[0] / (double)gaps.Sum():P0}, a triplet's 8th {gaps[1] / (double)gaps.Sum():P0}, an 8th {gaps[2] / (double)gaps.Sum():P0}, longer {gaps[3] / (double)gaps.Sum():P0}");
        Console.WriteLine(report);
        await Task.CompletedTask;
    }
}

using Rmg.Core.Composition;
using Rmg.Core.Songs;
using Rmg.Tests.Genres;

namespace Rmg.Tests.Forms;

/// <summary>Stop time: a section's first phrase, its band hitting the bar's first beat alone under the lines, which go on.</summary>
public sealed class StopTimeTest
{
    /// <summary>
    ///     In stop time the band hits the bar's first beat and rests, under a part that goes on: every bar sounds past its
    ///     hit, and in a bar the band stops in, only the lines and the drums keeping time play past the hit, the drums' fill
    ///     out of the phrase aside; a phrase with a bar nothing would carry is played as it was.
    /// </summary>
    [Test]
    public async Task InStopTime_TheBandHitsOnceABar_UnderAPartThatGoesOn()
    {
        int bars = 0, stopped = 0, timed = 0, melodyNotes = 0;
        foreach (var song in TestCorpus.InParallel(Enumerable.Range(0, 64), seed => TestCorpus.Get(seed, new SongOverrides(Genre: GenrePresets.Genre("blues")))))
        {
            var all = song.Song.Notes!.Values.SelectMany(x => x).ToArray();
            bool IsLine(int track) => song.Song.TrackDefinitions[track].Role is TrackRole.Melody or TrackRole.CounterMelody or TrackRole.Solo;
            bool KeepsTime(int track) => song.Song.TrackDefinitions[track] is PercussionInstrumentTrack definition && DrumRoles.Of(definition.StateMap) == DrumRole.Time;
            foreach (var (start, end, bar) in StopTimeSpans(song))
            for (var at = start; at < end - 1e-9; at += bar)
            {
                var (from, to) = (at + StopTime.Hit, Math.Min(end, at + bar));
                bars++;
                var because = $"seed {song.Seed} at {at}";
                // a note sounding past the hit, or the drums keeping time after the bar's first beat, as a short last bar leaves
                // little past its hit
                await Assert.That(all.Any(x => x.Position < to - 1e-9 && x.Position + x.Value.Duration > from + 1e-9)
                                  || song.Song.Notes!.Where(x => KeepsTime(x.Key)).Any(x => x.Value.Any(n => n.Position > at + 1e-6 && n.Position < to - 1e-9))).IsTrue().Because(because);
                // the drums' fill out of the section, in its last bar, which goes on as a drummer fills into the next, and the
                // drums and the bass in every phrase's last half bar, leading the band back in (StopTime)
                var section = song.Song.Map!.Sections.Last(x => x.Start <= at + 1e-9);
                // its last bar, a short one's
                var fill = section.LastBar;
                bool Leads(int track, double position) => song.Song.TrackDefinitions[track].Role switch
                {
                    TrackRole.Drum => position >= fill - 1e-9 || position >= end - bar / 2 - 1e-9,
                    TrackRole.Bass => position >= end - bar / 2 - 1e-9,
                    _ => false
                };
                // past the hit, but a line's, a time keeper's or a fill's
                var past = song.Song.Notes!.Where(x => !IsLine(x.Key) && !KeepsTime(x.Key))
                    .SelectMany(x => x.Value.Select(n => (Track: x.Key, Note: n)))
                    .Where(x => x.Note.Position >= at - 1e-9 && x.Note.Position < to - 1e-9 && !Leads(x.Track, x.Note.Position))
                    .Any(x => Math.Abs(x.Note.Position - at) > 1e-6 || x.Note.Value.Duration > StopTime.Hit + 1e-9);
                if (past)
                    continue;
                stopped++;
                melodyNotes += song.Song.Notes!.Where(x => IsLine(x.Key)).Sum(x => x.Value.Count(n => n.Position >= at - 1e-9 && n.Position < to - 1e-9));
                timed += song.Song.Notes!.Where(x => KeepsTime(x.Key)).Any(x => x.Value.Any(n => n.Position > at + 1e-6 && n.Position < to - 1e-9)) ? 1 : 0;
            }
        }

        Console.WriteLine($"of {bars} bars in stop time, {stopped} stopped, {timed} of them under the drums keeping time");
        await Assert.That(stopped).IsGreaterThan(bars / 2);
        await Assert.That(melodyNotes).IsGreaterThan(stopped);
    }

    /// <summary>
    ///     How much of a stopped bar's rest, after its hit, sounds, in Blues and Rock: the bars silent past their hit, and the
    ///     share of the rest that any part sounds through.
    /// </summary>
    [Test]
    [Explicit]
    public async Task Report()
    {
        foreach (var name in new[] { "blues", "rock" })
        {
            var (bars, silent, rest, sounding) = (0, 0, 0.0, 0.0);
            foreach (var song in TestCorpus.InParallel(Enumerable.Range(0, 64), seed => TestCorpus.Get(seed, new SongOverrides(Genre: GenrePresets.Genre(name)))))
            {
                var notes = song.Song.Notes!.Values.SelectMany(x => x).ToArray();
                foreach (var (start, end, bar) in StopTimeSpans(song))
                for (var at = start; at < end - 1e-9; at += bar)
                {
                    var (from, to) = (at + StopTime.Hit, Math.Min(end, at + bar));
                    bars++;
                    rest += to - from;
                    var covered = Covered(notes.Where(x => x.Position < to - 1e-9 && x.Position + x.Value.Duration > from + 1e-9)
                        .Select(x => (Math.Max(from, x.Position), Math.Min(to, x.Position + x.Value.Duration))));
                    sounding += covered;
                    silent += covered < 1e-9 ? 1 : 0;
                }
            }

            Console.WriteLine($"{name}: {bars} stopped bars, {silent} ({(double)silent / Math.Max(1, bars):P0}) silent past their hit, {sounding / Math.Max(1e-9, rest):P0} of their rest sounding");
        }

        await Assert.That(true).IsTrue();
    }

    /// <summary>The song's stop-time phrases, its start, end and bar, as the trace has them.</summary>
    private static IEnumerable<(double Start, double End, double Bar)> StopTimeSpans(CorpusSong song)
    {
        var stopping = song.Trace.Where(x => x.Point == TracePoints.StopTime && (bool)x.Value!).Select(x => x.Section).ToHashSet();
        var intro = song.Map.Intro;
        var built = intro.Kind == IntroKind.Entries && !intro.Window.IsBefore ? song.Map.Sections[0] : null;
        return song.Song.Map!.Sections.Where((x, i) => stopping.Contains(x.SectionId) && x != built && !song.Solos.ContainsKey(i))
            .Select(x => (x.Start, Math.Min(x.End, x.Start + x.Meter.PhraseDuration), x.Meter.BarDuration));
    }

    /// <summary>How long a set of spans covers, their overlaps once.</summary>
    private static double Covered(IEnumerable<(double From, double To)> spans)
    {
        var (total, reached) = (0.0, double.NegativeInfinity);
        foreach (var (from, to) in spans.OrderBy(x => x.Item1))
        {
            total += Math.Max(0, to - Math.Max(from, reached));
            reached = Math.Max(reached, to);
        }

        return total;
    }

    /// <summary>How often the drums fill the band back in where a stop-time phrase ends within its section, a tom or a snare struck in its last beat.</summary>
    [Test]
    [Explicit]
    public async Task FillsOutOfStopTime()
    {
        var fills = new[] { DrumGroups.Toms, DrumGroups.Snare }.SelectMany(x => x.Drums).Select(DrumGroups.GetTrackNumber).ToHashSet();
        var (ends, filled) = (0, 0);
        foreach (var name in new[] { "blues", "rock" })
        foreach (var song in TestCorpus.InParallel(Enumerable.Range(0, 64), seed => TestCorpus.Get(seed, new SongOverrides(Genre: GenrePresets.Genre(name)))))
        foreach (var (_, end, _) in StopTimeSpans(song).Where(x => song.Song.Map!.Sections.Any(s => x.End > s.Start + 1e-9 && x.End < s.End - 1e-9)))
        {
            ends++;
            var struck = song.Song.Notes!.Where(x => fills.Contains(x.Key)).SelectMany(x => x.Value).Count(x => x.Position >= end - 1 - 1e-9 && x.Position < end - 1e-9);
            filled += struck >= 2 ? 1 : 0;
        }

        Console.WriteLine($"stop-time phrases ending within their section {ends}, filled out of {filled / (double)Math.Max(1, ends):P0}");
        await Task.CompletedTask;
    }
}

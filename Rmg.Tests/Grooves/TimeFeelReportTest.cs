using Rmg.Core;
using Rmg.Core.Composition;

namespace Rmg.Tests.Grooves;

/// <summary>How the sections keep time: how many play in half time or double time, their energy, their drums' notes a bar and where the snare plays.</summary>
public sealed class TimeFeelReportTest
{
    [Test]
    [Explicit]
    public async Task Report()
    {
        var snares = DrumGroups.Snare.Drums.Select(DrumGroups.GetTrackNumber).ToHashSet();
        var stats = new Dictionary<int, (int Sections, int Bars, int Drums, int[] SnareBeats, double Energy)>();
        // the sections in four, whose beats the snare is counted on, each in its own meter
        foreach (var song in TestCorpus.Range(256))
        {
            var feels = song.Trace.Where(x => x.Point == TracePoints.TimeFeel).ToDictionary(x => x.Section, x => (int)x.Value!);
            var energy = song.Trace.Where(x => x.Point == TracePoints.SectionEnergy).ToDictionary(x => x.Section, x => ((SectionEnergyTrace)x.Value!).Energy);
            foreach (var span in song.Map.Sections.Where(x => x.Meter.HasSameBar(Meter.FourFour)))
            {
                var feel = feels[span.SectionId];
                var s = stats.GetValueOrDefault(feel, (0, 0, 0, new int[4], 0));
                s.Sections++;
                s.Energy += energy[span.SectionId];
                foreach (var (track, notes) in song.Song.Notes!.Where(x => song.IsDrum(x.Key)))
                foreach (var note in notes.Where(x => x.Position >= span.Start && x.Position < span.End))
                {
                    s.Drums++;
                    var beat = (note.Position - span.Start) % 4;
                    if (snares.Contains(track) && Math.Abs(beat - Math.Round(beat)) < 1e-6)
                        s.SnareBeats[(int)Math.Round(beat) % 4]++;
                }
                s.Bars += span.BarCount;
                stats[feel] = s;
            }
        }

        foreach (var (feel, s) in stats.OrderBy(x => x.Key))
            Console.WriteLine($"feel {feel}: {s.Sections} sections, energy {s.Energy / s.Sections:F2}, drums {s.Drums / (double)s.Bars:F1} a bar, snare on beats 1-4 {string.Join(" ", s.SnareBeats.Select(x => $"{x / (double)s.SnareBeats.Sum():P0}"))}");
        await Task.CompletedTask;
    }

    /// <summary>
    ///     A section in half time plays its backbeat on 3, a pinned one too, as Drum and bass pins its snare: its snare's
    ///     hits in the bars before the phrase's last mostly on the bar's third beat, rarely on its second and fourth.
    /// </summary>
    [Test]
    public async Task AHalfTimeSection_PlaysItsPinnedBackbeatOnThree()
    {
        var snares = DrumGroups.Snare.Tracks.ToHashSet();
        var (onThree, onTwoAndFour) = (0, 0);
        foreach (var song in TestCorpus.InParallel(Enumerable.Range(0, 64), seed => TestCorpus.Get(seed, new SongOverrides(Base: 0.5, Genre: Genres.GenrePresets.Genre("dnb")))))
        {
            var feels = song.Trace.Where(x => x.Point == TracePoints.TimeFeel).ToDictionary(x => x.Section, x => (int)x.Value!);
            // the snare pinned there, and outside the build into a drop, which rolls on the beats
            var pinned = song.Trace.Where(x => x.Point == TracePoints.BarPattern && snares.Contains(x.Track) && x.StateMap.GetStateValue(CompositionStateKinds.Pinned) > 0).Select(x => x.Section).ToHashSet();
            var drops = song.Trace.Where(x => x.Point == TracePoints.Drop).Select(x => (double)x.Value!).ToArray();
            foreach (var span in song.Map.Sections.Where(x => feels[x.SectionId] == 1 && pinned.Contains(x.SectionId) && x.Meter.HasSameBar(Meter.FourFour) && song.HasDrums(x)))
            foreach (var note in song.Song.Notes!.Where(x => snares.Contains(x.Key)).SelectMany(x => x.Value).Where(x => x.IsWithin(span.Start, span.End)))
            {
                var bar = span.BarOf(note.Position);
                if (bar % span.Meter.PhraseBars == span.Meter.PhraseBars - 1 || note.Value.Velocity < 0.5 || drops.Any(x => note.Position >= x - FormLayers.BuildBars * span.Meter.BarDuration - 1e-9 && note.Position < x))
                    continue;
                var beat = song.Map.BeatInBar(note.Position);
                onThree += Math.Abs(beat - 2) < 1e-6 ? 1 : 0;
                onTwoAndFour += Math.Abs(beat - 1) < 1e-6 || Math.Abs(beat - 3) < 1e-6 ? 1 : 0;
            }
        }

        Console.WriteLine($"half-time sections' snare: {onThree} on 3, {onTwoAndFour} on 2 and 4");
        await Assert.That(onThree).IsGreaterThan(20);
        await Assert.That(onThree).IsGreaterThan(onTwoAndFour * 2);
    }
}

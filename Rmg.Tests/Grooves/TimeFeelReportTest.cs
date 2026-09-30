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
        foreach (var song in TestCorpus.Range(200))
        {
            var feels = song.Trace.Where(x => x.Point == TracePoints.TimeFeel).ToDictionary(x => x.Section, x => (int)x.Value!);
            var energy = song.Trace.Where(x => x.Point == TracePoints.SectionEnergy).ToDictionary(x => x.Section, x => ((SectionEnergyTrace)x.Value!).Energy);
            foreach (var span in song.Map.Sections)
            {
                var feel = feels[span.SectionId];
                var s = stats.GetValueOrDefault(feel, (0, 0, 0, new int[4], 0));
                s.Sections++;
                s.Energy += energy[span.SectionId];
                foreach (var (track, notes) in song.Song.Notes!.Where(x => x.Key >= DrumGroups.FirstTrackNumber))
                foreach (var note in notes.Where(x => x.Position >= span.Start && x.Position < span.End))
                {
                    s.Drums++;
                    var beat = (note.Position - span.Start) % 4;
                    if (snares.Contains(track) && Math.Abs(beat - Math.Round(beat)) < 1e-6)
                        s.SnareBeats[(int)Math.Round(beat) % 4]++;
                }
                s.Bars += (int)(span.Duration / 4);
                stats[feel] = s;
            }
        }

        foreach (var (feel, s) in stats.OrderBy(x => x.Key))
            Console.WriteLine($"feel {feel}: {s.Sections} sections, energy {s.Energy / s.Sections:F2}, drums {s.Drums / (double)s.Bars:F1} a bar, snare on beats 1-4 {string.Join(" ", s.SnareBeats.Select(x => $"{x / (double)s.SnareBeats.Sum():P0}"))}");
        await Task.CompletedTask;
    }
}

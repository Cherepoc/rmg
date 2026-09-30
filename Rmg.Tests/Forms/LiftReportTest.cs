using Rmg.Core.Composition;

namespace Rmg.Tests.Forms;

/// <summary>How much louder the band plays in the bar before a section change than in the bar before that, into louder sections and quieter ones.</summary>
public sealed class LiftReportTest
{
    [Test]
    [Explicit]
    public async Task Report()
    {
        var (louder, quieter) = (new List<double>(), new List<double>());
        foreach (var song in TestCorpus.Range(200))
        {
            var energy = song.Trace.Where(x => x.Point == TracePoints.SectionEnergy).ToDictionary(x => x.Section, x => ((SectionEnergyTrace)x.Value!).Energy);
            var notes = song.Rendered.Tracks.SelectMany(x => x.NoteTimeline).ToArray();
            double Loudness(double from, double to) => notes.Where(x => x.Position >= from && x.Position < to).Select(x => x.Value.Velocity * 127).DefaultIfEmpty(double.NaN).Average();
            var sections = song.Map.Sections;
            for (var i = 1; i < sections.Length; i++)
            {
                var line = sections[i].Start;
                var step = Loudness(line - Meter.FourFour.BarDuration, line) - Loudness(line - 2 * Meter.FourFour.BarDuration, line - Meter.FourFour.BarDuration);
                if (double.IsNaN(step))
                    continue;
                (energy[sections[i].SectionId] > energy[sections[i - 1].SectionId] ? louder : quieter).Add(step);
            }
        }

        Console.WriteLine($"the bar before a change against the bar before it, in velocity: into a louder section {louder.Average():F1} over {louder.Count}, into a quieter {quieter.Average():F1} over {quieter.Count}");
        await Task.CompletedTask;
    }
}

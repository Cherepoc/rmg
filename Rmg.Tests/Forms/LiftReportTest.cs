using Rmg.Core.Composition;

namespace Rmg.Tests.Forms;

/// <summary>How much louder the band plays in the bar before a section change than in the bar before that, into louder sections and quieter ones.</summary>
public sealed class LiftReportTest
{
    [Test]
    public async Task IntoABiggerSection_TheHiHatOpensOnTheLastBeat_AndTheSnareStrikesItsHead()
    {
        var (cues, opened) = (0, 0);
        var hiHat = DrumGroups.GetTrackNumber(DrumDefinitions.HiHat);
        int[] snares = [DrumGroups.GetTrackNumber(DrumDefinitions.AcousticSnare), DrumGroups.GetTrackNumber(DrumDefinitions.ElectricSnare)];
        foreach (var song in TestCorpus.Range(128))
        {
            var lines = song.Trace.Where(x => x.Point == TracePoints.LiftCue).Select(x => (double)x.Value!).ToArray();
            foreach (var line in lines)
            {
                var bar = song.Map.MeterAt(line - 1e-6).BarDuration;
                // the open hi-hat, 46, on the last beat, and the snare's head, 38 or 40, over the last bar
                var open = song.Song.Notes!.GetValueOrDefault(hiHat, Rmg.Core.Events.EventTimeline.Create<Rmg.Core.Songs.RealizedNote>(0)).Where(x => x.Position >= line - 1 - 1e-9 && x.Position < line - 1e-9).ToArray();
                foreach (var note in open)
                    await Assert.That(note.Value.Pitches[0]).IsEqualTo(46).Because($"seed {song.Seed} at {note.Position}");
                opened += open.Length > 0 ? 1 : 0;
                foreach (var note in snares.SelectMany(t => song.Song.Notes!.GetValueOrDefault(t, Rmg.Core.Events.EventTimeline.Create<Rmg.Core.Songs.RealizedNote>(0))).Where(x => x.Position >= line - bar - 1e-9 && x.Position < line - 1e-9))
                    await Assert.That(note.Value.Pitches[0]).IsNotEqualTo(37).Because($"seed {song.Seed} at {note.Position}");
                cues++;
            }
        }

        // the hi-hat opening where it plays the last beat, which a third of the cues have, the rest keeping time on another
        Console.WriteLine($"{opened} of {cues} cues opening the hi-hat");
        await Assert.That(cues).IsGreaterThan(20);
        await Assert.That(opened).IsGreaterThan(cues / 5);
    }

    [Test]
    [Explicit]
    public async Task Report()
    {
        var steps = TestCorpus.Measure(256, song =>
        {
            var (louder, quieter) = (new List<double>(), new List<double>());
            var energy = song.Trace.Where(x => x.Point == TracePoints.SectionEnergy).ToDictionary(x => x.Section, x => ((SectionEnergyTrace)x.Value!).Energy);
            var notes = song.Rendered.Tracks.SelectMany(x => x.NoteTimeline).ToArray();
            double Loudness(double from, double to) => notes.Where(x => x.Position >= from && x.Position < to).Select(x => x.Value.Velocity * 127).DefaultIfEmpty(double.NaN).Average();
            var sections = song.Map.Sections;
            for (var i = 1; i < sections.Length; i++)
            {
                var line = sections[i].Start;
                // the bar before the line, the bar there is, a short last bar's, against the section's full bar before it
                var (last, bar) = (song.Map.BarBefore(line), sections[i - 1].Meter.BarDuration);
                var step = Loudness(line - last, line) - Loudness(line - last - bar, line - last);
                if (double.IsNaN(step))
                    continue;
                (energy[sections[i].SectionId] > energy[sections[i - 1].SectionId] ? louder : quieter).Add(step);
            }

            return (Louder: louder, Quieter: quieter);
        });
        var (louder, quieter) = (steps.SelectMany(x => x.Louder).ToList(), steps.SelectMany(x => x.Quieter).ToList());

        Console.WriteLine($"the bar before a change against the bar before it, in velocity: into a louder section {louder.Average():F1} over {louder.Count}, into a quieter {quieter.Average():F1} over {quieter.Count}");
        await Task.CompletedTask;
    }
}

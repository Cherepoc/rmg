using Rmg.Core.Composition;
using Rmg.Core.Rendering;

namespace Rmg.Tests.SongGenerators;

public sealed class SongGeneratorVelocityTest
{
    [Test]
    public async Task NotesAtBarStart_AreLouder_ThanNotesOffTheEighthGrid()
    {
        // the accent follows each pattern's own beats, not the bar's, so this only sees part of it: over 128 songs the bar
        // start is about 4.5 steps of 127 louder, a loud note's accent held back as the scale holds it, over fewer too
        // unsteady to tell
        var barStart = new List<double>();
        var offGrid = new List<double>();
        foreach (var seed in Enumerable.Range(0, 128))
        {
            // the bars of the song's meter, from the first section on
            var (bar, origin) = (TestCorpus.Get(seed).Map.Meter.BarDuration, TestCorpus.Get(seed).Origin);
            // but a pad, which holds its chords evenly, with no accent to measure
            foreach (var track in TestCorpus.Get(seed).Rendered.Tracks.Where(x => x.Role != TrackRole.Pad))
            {
                foreach (var note in track.NoteTimeline)
                {
                    if (note.Position >= origin && IsOnGrid(note.Position - origin, bar))
                        barStart.Add(note.Value.Velocity);
                    else if (!IsOnGrid(note.Position - origin, 0.5))
                        offGrid.Add(note.Value.Velocity);
                }
            }
        }

        Console.WriteLine($"bar start {(barStart.Average() - offGrid.Average()) * 127:F2} steps louder");
        await Assert.That(barStart.Average() - offGrid.Average()).IsGreaterThan(3.5 / 127);
    }

    private static bool IsOnGrid(double position, double step)
    {
        var remainder = position % step;
        return remainder < 1e-6 || step - remainder < 1e-6;
    }
}

using Rmg.Core.Composition;
using Rmg.Core.Rendering;

namespace Rmg.Tests.SongGenerators;

public sealed class SongGeneratorVelocityTest
{
    [Test]
    public async Task NotesAtBarStart_AreLouder_ThanNotesOffTheEighthGrid()
    {
        // the accent follows each pattern's own beats, not the bar's, so this only sees part of it: over 256 songs the bar
        // start is about 6 steps of 127 louder, over fewer too unsteady to tell
        var barStart = new List<double>();
        var offGrid = new List<double>();
        foreach (var seed in Enumerable.Range(0, 128))
        {
            // a pad holds its chords evenly, with no accent to measure
            var pad = ((Rmg.Core.Songs.PitchInstrumentTrack)TestCorpus.Get(seed).Song.TrackDefinitions[Rmg.Core.Composition.SongTracks.PadTrack]).InstrumentCode;
            // the bars of the song's meter, from the first section on
            var (bar, origin) = (TestCorpus.Get(seed).Map.Meter.BarDuration, TestCorpus.Get(seed).Origin);
            foreach (var track in TestCorpus.Get(seed).Rendered.Tracks.Where(x => x.IsPercussionInstrument || x.PitchInstrumentCode != pad))
            {
                foreach (var note in track.NoteTimeline)
                {
                    if (note.Position >= origin && IsOnGrid(note.Position - origin, bar))
                        barStart.Add(note.Value.Velocity);
                    else if (!IsOnGrid(note.Position, 0.5))
                        offGrid.Add(note.Value.Velocity);
                }
            }
        }

        await Assert.That(barStart.Average() - offGrid.Average()).IsGreaterThan(5.0 / 127);
    }

    private static bool IsOnGrid(double position, double step)
    {
        var remainder = position % step;
        return remainder < 1e-6 || step - remainder < 1e-6;
    }
}

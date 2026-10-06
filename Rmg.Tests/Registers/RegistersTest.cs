using Rmg.Core.Composition;
using Rmg.Core.Songs;

namespace Rmg.Tests.Registers;

public sealed class RegistersTest
{
    [Test]
    public async Task EveryPart_SitsInItsInstrumentsOctaves()
    {
        foreach (var song in TestCorpus.Range(64))
        foreach (var part in song.Song.TrackDefinitions.Values.OfType<PitchInstrumentTrack>())
        {
            var (low, high) = Rmg.Core.Composition.Registers.OctavesOf(part.InstrumentCode);
            await Assert.That(part.MinOctaveOffset >= low && part.MaxOctaveOffset <= high && part.MinOctaveOffset <= part.MaxOctaveOffset).IsTrue()
                .Because($"seed {song.Seed}, {part.Role} on {GeneralMidi.Programs[part.InstrumentCode]} in {part.MinOctaveOffset}..{part.MaxOctaveOffset}");
        }
    }
}

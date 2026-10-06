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

    [Test]
    public async Task AGenresRegister_MovesAPartAnOctave_ItsInstrumentWithIt()
    {
        var down = new Genre(System.Collections.Immutable.ImmutableDictionary<string, System.Collections.Immutable.ImmutableDictionary<string, Rmg.Core.Probabilities.ByConvention>>.Empty
            .Add("register", System.Collections.Immutable.ImmutableDictionary<string, Rmg.Core.Probabilities.ByConvention>.Empty.Add("riff", new(0, 0, 0))));
        foreach (var seed in Enumerable.Range(0, 16))
        {
            var (song, lowered) = (TestCorpus.Get(seed), TestCorpus.Get(seed, new SongOverrides(Genre: down)));
            var riff = (PitchInstrumentTrack)song.Song.TrackDefinitions[SongTracks.RiffTrack];
            var low = (PitchInstrumentTrack)lowered.Song.TrackDefinitions[SongTracks.RiffTrack];
            await Assert.That(low.MinOctaveOffset).IsEqualTo(riff.MinOctaveOffset - 1).Because($"seed {seed}");
            await Assert.That(((PitchInstrumentTrack)lowered.Song.TrackDefinitions[SongTracks.RiffTwinTrack]).MinOctaveOffset).IsEqualTo(low.MinOctaveOffset - 0).Because($"seed {seed}");
        }
    }
}

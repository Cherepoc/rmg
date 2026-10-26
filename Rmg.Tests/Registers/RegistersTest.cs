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

    /// <summary>
    ///     A rule: every note of a line on its part's own instrument within the instrument's range, a bass's below its low E
    ///     an octave up (<see cref="Realizer.RangeOf" />), but a raised line's and a solo's, which sit above the part's.
    /// </summary>
    [Test]
    public async Task EveryLinesNote_OnItsOwnInstrument_IsInTheInstrumentsRange()
    {
        foreach (var song in TestCorpus.Range(64))
        foreach (var (track, notes) in song.Song.Notes!)
        {
            if (song.Song.TrackDefinitions[track] is not PitchInstrumentTrack part || !part.Role.PlaysLine())
                continue;
            var (low, high) = InstrumentRanges.Of(part.InstrumentCode);
            foreach (var note in notes.Where(x => InstrumentsOverTime.ProgramOf(x.Value.State, part.InstrumentCode) == part.InstrumentCode
                                                  && !x.Value.State.GetStateValue(CompositionStateKinds.Instrument).IsSet
                                                  && x.Value.State.GetStateValue(CompositionStateKinds.LineRaise) == 0 && x.Value.State.GetStateValue(CompositionStateKinds.LineSolo) == 0))
                await Assert.That(note.Value.Pitches.All(x => x >= low && x <= high)).IsTrue()
                    .Because($"seed {song.Seed}, {part.Role} on {GeneralMidi.Programs[part.InstrumentCode]} at {note.Position}: {string.Join(" ", note.Value.Pitches)} in {low}-{high}");
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
            var riff = (PitchInstrumentTrack)song.Song.TrackDefinitions[song.Song.Track(TrackRole.Riff)];
            var low = (PitchInstrumentTrack)lowered.Song.TrackDefinitions[lowered.Song.Track(TrackRole.Riff)];
            // the part's whole range an octave down, its twin's with it
            await Assert.That(low.MinOctaveOffset).IsEqualTo(riff.MinOctaveOffset - 1).Because($"seed {seed}");
            await Assert.That(low.MaxOctaveOffset).IsEqualTo(riff.MaxOctaveOffset - 1).Because($"seed {seed}");
            await Assert.That(((PitchInstrumentTrack)lowered.Song.TrackDefinitions[lowered.Song.Track(TrackRole.RiffTwin)]).MinOctaveOffset).IsEqualTo(low.MinOctaveOffset).Because($"seed {seed}");
        }
    }
}

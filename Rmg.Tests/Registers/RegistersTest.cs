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
    ///     A rule: every note of a line on its part's own instrument within the instrument's range as the part tunes it, a bass's below its low E
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
            var (low, high) = Rmg.Core.Composition.Registers.RangeOf(part);
            foreach (var note in notes.Where(x => InstrumentsOverTime.ProgramOf(x.Value.State, part.InstrumentCode) == part.InstrumentCode
                                                  && !x.Value.State.GetStateValue(CompositionStateKinds.Instrument).IsSet
                                                  && x.Value.State.GetStateValue(CompositionStateKinds.LineRaise) == 0 && x.Value.State.GetStateValue(CompositionStateKinds.LineSolo) == 0))
                await Assert.That(note.Value.Pitches.All(x => x >= low && x <= high)).IsTrue()
                    .Because($"seed {song.Seed}, {part.Role} on {GeneralMidi.Programs[part.InstrumentCode]} at {note.Position}: {string.Join(" ", note.Value.Pitches)} in {low}-{high}");
        }
    }

    /// <summary>
    ///     A rule: a genre's tuning moves its parts' ranges by its semitones, Djent's guitars by the song's sound from E down to
    ///     E an octave below from the middle on, its bass with them as far as B0 (<see cref="Rmg.Core.Composition.Registers.LowestTuned" />), and
    ///     every note of their lines on their own instruments within the tuned range, the same seeds' parts in standard tuning
    ///     otherwise.
    /// </summary>
    [Test]
    public async Task AGenresTuning_MovesItsPartsRanges_EveryNoteWithin()
    {
        var djent = Rmg.Tests.Genres.GenrePresets.Genre("djent");
        var standard = djent with { Choices = djent.Choices.SetItem(GenreChoices.Tuning.Name, System.Collections.Immutable.ImmutableDictionary<string, Rmg.Core.Probabilities.ByConvention>.Empty) };
        var tunings = new HashSet<int>();
        foreach (var seed in Enumerable.Range(0, 16))
        {
            var (song, plain) = (TestCorpus.Get(seed, new SongOverrides(Genre: djent)), TestCorpus.Get(seed, new SongOverrides(Genre: standard)));
            var riffTuning = ((PitchInstrumentTrack)song.Song.TrackDefinitions[song.Song.Track(TrackRole.Riff)]).StateMap.GetStateValue(CompositionStateKinds.Tuning);
            tunings.Add(riffTuning);
            await Assert.That(riffTuning).IsBetween(-12, -1).Because($"seed {seed}");
            foreach (var role in new[] { TrackRole.Riff, TrackRole.Bass })
            {
                var (tuned, untuned) = ((PitchInstrumentTrack)song.Song.TrackDefinitions[song.Song.Track(role)], (PitchInstrumentTrack)plain.Song.TrackDefinitions[plain.Song.Track(role)]);
                var tuning = Math.Max(riffTuning, Rmg.Core.Composition.Registers.LowestTuned - InstrumentRanges.Of(tuned.InstrumentCode).Low);
                await Assert.That(Rmg.Core.Composition.Registers.RangeOf(tuned).Low).IsEqualTo(InstrumentRanges.Of(tuned.InstrumentCode).Low + tuning).Because($"seed {seed}, {role}");
                await Assert.That(Realizer.GetRange(tuned).Low).IsEqualTo(Realizer.GetRange(untuned).Low + tuning).Because($"seed {seed}, {role}");
            }

            foreach (var (track, notes) in song.Song.Notes!)
            {
                if (song.Song.TrackDefinitions[track] is not PitchInstrumentTrack part || !part.Role.PlaysLine())
                    continue;
                var (low, high) = Rmg.Core.Composition.Registers.RangeOf(part);
                foreach (var note in notes.Where(x => InstrumentsOverTime.ProgramOf(x.Value.State, part.InstrumentCode) == part.InstrumentCode
                                                      && !x.Value.State.GetStateValue(CompositionStateKinds.Instrument).IsSet
                                                      && x.Value.State.GetStateValue(CompositionStateKinds.LineRaise) == 0 && x.Value.State.GetStateValue(CompositionStateKinds.LineSolo) == 0))
                    await Assert.That(note.Value.Pitches.All(x => x >= low && x <= high)).IsTrue()
                        .Because($"seed {seed}, {part.Role} at {note.Position}: {string.Join(" ", note.Value.Pitches)} in {low}-{high}");
            }
        }

        // drawn by the song, not one for the genre
        await Assert.That(tunings.Count).IsGreaterThan(2);
    }

    /// <summary>
    ///     A rule: a chord part a genre moves an octave down, the metal rhythm part, plays its chords open, every note of a
    ///     chord its root's unison, fifth or octave, no third nor colour (<see cref="CompositionStateKinds.OpenChords" />); a
    ///     note doubling the riff's line plays the riff's.
    /// </summary>
    [Test]
    public async Task AChordPartMovedDown_PlaysItsChordsOpen_NoThird()
    {
        var metal = Rmg.Tests.Genres.GenrePresets.Genre("metal");
        var chords = 0;
        foreach (var song in TestCorpus.Streamed(Enumerable.Range(0, 32), seed => TestCorpus.Get(seed, new SongOverrides(Genre: metal))))
        {
            if (!song.Song.TrackDefinitions.Values.Any(x => x.Role == TrackRole.Rhythm))
                continue;
            var track = song.Song.Track(TrackRole.Rhythm);
            await Assert.That(((PitchInstrumentTrack)song.Song.TrackDefinitions[track]).StateMap.GetStateValue(CompositionStateKinds.OpenChords)).IsEqualTo(1).Because($"seed {song.Seed}");
            foreach (var note in (song.Song.Notes!.GetValueOrDefault(track)?.ToArray() ?? []).Where(x => x.Value.Pitches.Length > 1 && x.Value.State.GetStateValue(CompositionStateKinds.Doubled) == 0
                                                                     && x.Value.State.GetStateValue(CompositionStateKinds.LineSolo) == 0))
            {
                chords++;
                await Assert.That(note.Value.Pitches.All(x => (x - note.Value.Pitches.Min()) is 0 or 7 or 12 or 19 or 24)).IsTrue()
                    .Because($"seed {song.Seed} at {note.Position}: {string.Join(" ", note.Value.Pitches)}");
            }
        }

        await Assert.That(chords).IsGreaterThan(0);
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

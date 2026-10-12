using System.Collections.Immutable;
using Rmg.Core;
using Rmg.Core.Composition;
using Rmg.Core.Events;
using Rmg.Core.Probabilities;
using Rmg.Core.Songs;

namespace Rmg.Tests.Chords;

/// <summary>Held tones (<see cref="ChordColour.Held" />): the key's steps held over every broken chord, on a clean instrument that dies away.</summary>
public sealed class HeldTonesTest
{
    /// <summary>A genre whose chords break on the instrument given, every section, in the colour given.</summary>
    private static Genre Holding(ChordColour colour, string instrument) => new(ImmutableDictionary<string, ImmutableDictionary<string, ByConvention>>.Empty
        .Add(GenreChoices.Arpeggio.Name, ImmutableDictionary<string, ByConvention>.Empty.Add(GenreChoices.Name(nameof(ArpeggioPattern.Up)), new ByConvention(1, 1, 1)))
        .Add(GenreChoices.Colour.Name, ImmutableDictionary<string, ByConvention>.Empty.Add(GenreChoices.Name(colour.ToString()), new ByConvention(1, 1, 1)))
        .Add(GenreChoices.Instruments[TrackRole.Chords].Name, ImmutableDictionary<string, ByConvention>.Empty.Add(GenreChoices.Name(instrument), new ByConvention(1, 1, 1))));

    /// <summary>The broken chords' notes, each with its chord's own classes, of the songs given.</summary>
    private static (RealizedNote Note, IReadOnlySet<int> Chord)[] Broken(IEnumerable<CorpusSong> songs) =>
    [
        ..songs.SelectMany(song => song.Song.Notes![song.Song.Track(TrackRole.Chords)]
            .Where(x => x.Value.State.GetStateValue(CompositionStateKinds.Arpeggio) != 0 && x.Value.State.GetStateValue(CompositionStateKinds.LineSolo) == 0 && x.Value.Pitches.Length == 1)
            .Select(x => (x.Value, Realizer.GetChordNotes(x.Value.State).Classes)))
    ];

    /// <summary>A held note, one of the chord's own or the key's steps held, a semitone from a chord's note or not.</summary>
    private static (int Held, int Rubbing) Measure((RealizedNote Note, IReadOnlySet<int> Chord)[] notes)
    {
        var held = notes.Where(x => !x.Chord.Contains(x.Note.Pitches[0].Mod(12))).ToArray();
        return (held.Length, held.Count(x => x.Chord.Any(c => Math.Abs((c - x.Note.Pitches[0]).Mod(12) - 6) == 5)));
    }

    [Test]
    [Arguments(ChordColour.Held)]
    [Arguments(ChordColour.HeldDark)]
    public async Task HeldTones_RingOverTheBrokenChords_OnACleanGuitar_NeverADrivenOne(ChordColour colour)
    {
        var clean = TestCorpus.InParallel(Enumerable.Range(0, 64), seed => TestCorpus.Get(seed, new SongOverrides(Genre: Holding(colour, "Electric Guitar (clean)"))));
        var driven = TestCorpus.InParallel(Enumerable.Range(0, 64), seed => TestCorpus.Get(seed, new SongOverrides(Genre: Holding(colour, "Distortion Guitar"))));

        // every note the chord's, a note of its own scale, or a held step of the key, the key's own as the section has it
        foreach (var song in clean)
        // but a solo's line, which the part may play
        foreach (var note in song.Song.Notes![song.Song.Track(TrackRole.Chords)].Where(x => x.Value.State.GetStateValue(CompositionStateKinds.Arpeggio) != 0
                                                                                           && x.Value.State.GetStateValue(CompositionStateKinds.LineSolo) == 0))
        {
            var (own, root, chord) = Realizer.GetChord(note.Value.State);
            var scale = note.Value.State.GetStateValue(StateKinds.ScaleOffsets);
            var tonic = note.Value.State.GetStateValue(StateKinds.KeyOffset);
            var key = scale.Select(x => (tonic + x).Mod(12)).Concat(Enumerable.Range(0, own.Length).Select(x => chord.GetPitch(x - root).Mod(12))).ToHashSet();
            var classes = Realizer.GetChordNotes(note.Value.State).Classes;
            foreach (var pitch in note.Value.Pitches)
                await Assert.That(classes.Contains(pitch.Mod(12)) || key.Contains(pitch.Mod(12))).IsTrue().Because($"seed {song.Seed} at {note.Position}");
        }

        // the notes off the chord against the same songs with no colour, which other devices play a few of
        var plain = TestCorpus.InParallel(Enumerable.Range(0, 64), seed => TestCorpus.Get(seed, new SongOverrides(Genre: Holding(ChordColour.None, "Electric Guitar (clean)"))));
        var (held, rubbing) = Measure(Broken(clean));
        var (broken, plainBroken) = (Broken(clean).Length, Broken(plain).Length);
        var (share, plainShare) = (held / (double)broken, Measure(Broken(plain)).Held / (double)plainBroken);
        Console.WriteLine($"{colour}: {share:P1} of {broken} broken notes off the chord, {rubbing} a semitone from a chord's note, against {plainShare:P1} with no colour");
        // measured 16% to 18.5% off the chord against none with no colour, at offsets 0 and a million
        await Assert.That(share).IsGreaterThan(plainShare + 0.08);
        // a driven guitar holds none, its notes off the chord as with no colour
        var drivenPlain = TestCorpus.InParallel(Enumerable.Range(0, 64), seed => TestCorpus.Get(seed, new SongOverrides(Genre: Holding(ChordColour.None, "Distortion Guitar"))));
        await Assert.That(Measure(Broken(driven)).Held).IsEqualTo(Measure(Broken(drivenPlain)).Held);
    }
}

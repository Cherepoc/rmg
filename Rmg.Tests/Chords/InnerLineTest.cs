using System.Collections.Immutable;
using Rmg.Core;
using Rmg.Core.Composition;
using Rmg.Core.Songs;

namespace Rmg.Tests.Chords;

/// <summary>A chromatic line inside a held chord: its root voice falling a semitone a quarter, the root, the major seventh, the seventh, the sixth, as the song's state.</summary>
public sealed class InnerLineTest
{
    [Test]
    public async Task ALinesNotes_FallFromTheChordsRoot_ASemitoneAQuarter()
    {
        var lined = 0;
        foreach (var song in TestCorpus.Streamed(Enumerable.Range(0, 32), seed => TestCorpus.Get(seed,
                     new SongOverrides(Facets: TestCorpus.FacetsOf((Facet.Chords, 0.8))))))
        foreach (var note in song.Song.Notes![song.Song.Track(TrackRole.Chords)].Where(x => x.Value.State.GetStateValue(CompositionStateKinds.InnerLine) > 0))
        {
            lined++;
            var root = Realizer.GetChord(note.Value.State).Chord.Root.Mod(12);
            var falls = note.Value.State.GetStateValue(CompositionStateKinds.InnerLine);
            // the chord's notes the fallen one, 1 to 3 semitones under its root, where the root was, and the root no more
            var classes = Realizer.GetChordNotes(note.Value.State).Classes;
            await Assert.That(falls is >= 1 and <= 3).IsTrue();
            await Assert.That(classes.Contains((root - falls).Mod(12)) && !classes.Contains(root)).IsTrue()
                .Because($"seed {song.Seed} at {note.Position}: root {root}, falls {falls}, classes {string.Join(",", classes)}");
            await Assert.That(note.Value.Pitches.All(x => classes.Contains(x.Mod(12)))).IsTrue();
        }

        Console.WriteLine($"{lined} chords with a line inside");
        await Assert.That(lined).IsGreaterThan(10);
    }

    [Test]
    public async Task APlainSong_MovesNoLine()
    {
        foreach (var song in TestCorpus.Streamed(Enumerable.Range(0, 16), seed => TestCorpus.Get(seed,
                     new SongOverrides(Facets: TestCorpus.FacetsOf((Facet.Chords, 0))))))
            await Assert.That(song.Song.Notes!.Values.SelectMany(x => x).All(x => x.Value.State.GetStateValue(CompositionStateKinds.InnerLine) == 0)).IsTrue();
    }
}

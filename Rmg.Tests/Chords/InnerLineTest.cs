using System.Collections.Immutable;
using Rmg.Core;
using Rmg.Core.Composition;
using Rmg.Core.Songs;

namespace Rmg.Tests.Chords;

/// <summary>A chromatic line inside a held chord: its root voice falling a semitone a quarter, the root, the major seventh, the seventh, the sixth.</summary>
public sealed class InnerLineTest
{
    [Test]
    public async Task ALinesNotes_FallFromTheChordsRoot_ASemitoneAQuarter()
    {
        var lined = 0;
        foreach (var song in TestCorpus.InParallel(Enumerable.Range(0, 32), seed => TestCorpus.Get(seed,
                     new SongOverrides(Facets: ImmutableDictionary<Facet, double>.Empty.Add(Facet.Chords, 0.8)))))
        foreach (var note in song.Song.Notes![SongTracks.ChordsTrack].Where(x => x.Value.State.GetStateValue(CompositionStateKinds.InnerLine) > 0))
        {
            lined++;
            var root = Realizer.GetChord(note.Value.State).Chord.Root.Mod(12);
            // one voice of the chord 1 to 3 semitones under its root, where the root was
            await Assert.That(note.Value.Pitches.Any(x => (root - x).Mod(12) is 1 or 2 or 3)).IsTrue();
        }

        Console.WriteLine($"{lined} chords with a line inside");
        await Assert.That(lined).IsGreaterThan(10);
    }

    [Test]
    public async Task APlainSong_MovesNoLine()
    {
        foreach (var song in TestCorpus.InParallel(Enumerable.Range(0, 16), seed => TestCorpus.Get(seed,
                     new SongOverrides(Facets: ImmutableDictionary<Facet, double>.Empty.Add(Facet.Chords, 0)))))
            await Assert.That(song.Song.Notes!.Values.SelectMany(x => x).All(x => x.Value.State.GetStateValue(CompositionStateKinds.InnerLine) == 0)).IsTrue();
    }
}

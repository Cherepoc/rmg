using System.Collections.Immutable;
using Rmg.Core;
using Rmg.Core.Composition;
using Rmg.Core.Probabilities;
using Rmg.Core.Songs;

namespace Rmg.Tests.Chords;

/// <summary>A montuno (<see cref="ArpeggioPattern.Montuno" />): the root on the beats, the chord's other notes on the off-beats.</summary>
public sealed class MontunoTest
{
    private static Genre Montuno => new(ImmutableDictionary<string, ImmutableDictionary<string, ByConvention>>.Empty
        .Add(GenreChoices.Arpeggio.Name, ImmutableDictionary<string, ByConvention>.Empty.Add(GenreChoices.Name(nameof(ArpeggioPattern.Montuno)), new ByConvention(1, 1, 1)))
        .Add(GenreChoices.Instruments[TrackRole.Chords].Name, ImmutableDictionary<string, ByConvention>.Empty.Add(GenreChoices.Name("Acoustic Grand Piano"), new ByConvention(1, 1, 1))));

    [Test]
    public async Task AMontuno_PlaysTheRootOnTheBeats_TheOtherNotesOffThem()
    {
        var (beats, beatRoots, offs, offRoots) = (0, 0, 0, 0);
        foreach (var song in TestCorpus.InParallel(Enumerable.Range(0, 32), seed => TestCorpus.Get(seed, new SongOverrides(Genre: Montuno))))
        foreach (var note in song.Song.Notes![song.Song.Track(TrackRole.Chords)]
                     .Where(x => (ArpeggioPattern)x.Value.State.GetStateValue(CompositionStateKinds.Arpeggio) == ArpeggioPattern.Montuno
                                 && x.Value.State.GetStateValue(CompositionStateKinds.LineSolo) == 0 && x.Value.Pitches.Length == 1))
        {
            // the root of the note's own chord, a pushed note's the chord it anticipates
            var isRoot = note.Value.Pitches[0].Mod(12) == Realizer.GetChordNotes(note.Value.State).Chord.Root.Mod(12);
            if (Math.Abs(note.Position - Math.Round(note.Position)) < 1e-6)
                (beats, beatRoots) = (beats + 1, beatRoots + (isRoot ? 1 : 0));
            else
                (offs, offRoots) = (offs + 1, offRoots + (isRoot ? 1 : 0));
        }

        // but a voicing without its root, and a note pushed, an anticipation (measured: 99% and 3% to 5%, at offsets 0 and a million)
        Console.WriteLine($"montuno: {beatRoots} of {beats} notes on the beat the root, {offRoots} of {offs} off it");
        await Assert.That(beats).IsGreaterThan(100);
        await Assert.That(beatRoots).IsGreaterThan(beats * 8 / 10);
        await Assert.That(offRoots).IsLessThan(offs * 3 / 10);
    }
}

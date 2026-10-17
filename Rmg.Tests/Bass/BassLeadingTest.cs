using System.Collections.Immutable;
using Rmg.Core.Composition;
using Rmg.Core.Events;
using Rmg.Core.Probabilities;
using Rmg.Core.Songs;
using Rmg.Tests.Genres;

namespace Rmg.Tests.Bass;

/// <summary>A genre's bass: how often it leads into a chord (<see cref="GenreChoices.Bass" />), and what it lands on there (<see cref="GenreChoices.BassLanding" />).</summary>
public sealed class BassLeadingTest
{
    private static Genre With(GenreChoice choice, ImmutableDictionary<string, ByConvention> options) =>
        new(ImmutableDictionary<string, ImmutableDictionary<string, ByConvention>>.Empty.Add(choice.Name, options));

    /// <summary>The bass's notes before a change of chord that lead into it, and the changes it plays across.</summary>
    private static (int Led, int Changes) Leading(CorpusSong song)
    {
        var bass = song.Song.Notes!.GetValueOrDefault(song.Song.Track(TrackRole.Bass))?.ToArray() ?? [];
        var (led, changes) = (0, 0);
        foreach (var change in song.ChordChanges)
        {
            var before = Array.FindLastIndex(bass, x => x.Position < change - 1e-9);
            if (before < 0 || before + 1 >= bass.Length || Math.Abs(bass[before + 1].Position - change) > 1e-9)
                continue;
            changes++;
            led += bass[before].Value.State.GetStateValue(StateKinds.ChordApproach) != 0 ? 1 : 0;
        }

        return (led, changes);
    }

    /// <summary>A genre leading its bass into every chord it can leads into more of them than one leading into none, over the same songs.</summary>
    [Test]
    public async Task AGenresBassLeading_LeadsIntoMoreChords_ThanNone()
    {
        double Share(double leading)
        {
            var genre = With(GenreChoices.Bass, ImmutableDictionary<string, ByConvention>.Empty.Add("leading", new ByConvention(leading, leading, leading)));
            var measured = TestCorpus.InParallel(Enumerable.Range(0, 64), seed => Leading(TestCorpus.Get(seed, new SongOverrides(Genre: genre)))).ToArray();
            return measured.Sum(x => x.Led) / (double)measured.Sum(x => x.Changes);
        }

        var (most, none) = (Share(1), Share(0));
        // against the mechanism breaking (measured: 79% to 81% leaning most, 10% to 12% least, at offsets 0 and a million)
        Console.WriteLine($"changes the bass leads into: {most:P0} leaning most, {none:P0} leaning least");
        await Assert.That(most).IsGreaterThan(none + 0.1);
    }

    /// <summary>A genre's landings named root and fifth alone never land the bass on a chord's third.</summary>
    [Test]
    public async Task AGenresBassLanding_KeepsToItsPool()
    {
        var genre = With(GenreChoices.BassLanding, ImmutableDictionary<string, ByConvention>.Empty
            .Add(GenreChoices.Name(nameof(ChordArrival.Root)), new ByConvention(1, 1, 1))
            .Add(GenreChoices.Name(nameof(ChordArrival.Fifth)), new ByConvention(1, 1, 1)));
        var landings = 0;
        foreach (var song in TestCorpus.InParallel(Enumerable.Range(0, 16), seed => TestCorpus.Get(seed, new SongOverrides(Genre: genre))))
        foreach (var note in song.Song.Notes![song.Song.Track(TrackRole.Bass)])
        {
            var arrival = (ChordArrival)note.Value.State.GetStateValue(StateKinds.ChordArrival);
            await Assert.That(arrival).IsNotEqualTo(ChordArrival.Third).Because($"seed {song.Seed} at {note.Position}");
            landings += arrival is ChordArrival.Root or ChordArrival.Fifth ? 1 : 0;
        }

        await Assert.That(landings).IsGreaterThan(0);
    }

    /// <summary>A bass's inversion, its first note on a new chord, lands on a note of the chord, but on a pedal, which holds its note across the chords, on an inner line's step, a voice above the root it holds moving, and on a chord's one note that leads into the next, a sus chord's root where the third it lands on is left out.</summary>
    [Test]
    public async Task ABassInversion_LandsOnANoteOfItsChord()
    {
        var (landings, sus) = (0, 0);
        foreach (var song in TestCorpus.InParallel(Enumerable.Range(0, 32), seed => TestCorpus.Get(seed, new SongOverrides(Genre: GenrePresets.Genre("chamber")))))
        foreach (var note in song.Song.Notes![song.Song.Track(TrackRole.Bass)].Where(x => (ChordArrival)x.Value.State.GetStateValue(StateKinds.ChordArrival) is ChordArrival.Third or ChordArrival.Fifth
                                                                                      && song.ChordChanges.Any(c => Math.Abs(c - x.Position) < 1e-9)
                                                                                      && x.Value.State.GetStateValue(CompositionStateKinds.BassPedal) == 0
                                                                                      && x.Value.State.GetStateValue(StateKinds.ChordApproach) == 0
                                                                                      // nor an inner line's step, a voice above the root the bass holds moving
                                                                                      && x.Value.State.GetStateValue(CompositionStateKinds.InnerLine) == 0))
        {
            var (chord, _, classes) = Realizer.GetChordNotes(note.Value.State);
            landings++;
            sus += classes.Count(x => ((x - chord.Root) % 12 + 12) % 12 is 3 or 4) == 0 ? 1 : 0;
            await Assert.That(classes.Contains(note.Value.Pitches[0] % 12)).IsTrue().Because($"seed {song.Seed} at {note.Position}");
        }

        Console.WriteLine($"{landings} inversions, {sus} on chords without a third");
        await Assert.That(sus).IsGreaterThan(0);
    }
}

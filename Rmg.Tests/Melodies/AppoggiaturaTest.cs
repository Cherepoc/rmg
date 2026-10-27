using System.Collections.Immutable;
using Rmg.Core;
using Rmg.Core.Composition;
using Rmg.Core.Probabilities;
using Rmg.Core.Songs;

namespace Rmg.Tests.Melodies;

/// <summary>Appoggiaturas (<see cref="LinePattern.Appoggiaturas" />): a strong beat's note leaning on a tension, the next resolving it by step to the chord.</summary>
public sealed class AppoggiaturaTest
{
    private static Genre Leaning(double chance) => new(ImmutableDictionary<string, ImmutableDictionary<string, ByConvention>>.Empty
        .Add(GenreChoices.Appoggiatura.Name, ImmutableDictionary<string, ByConvention>.Empty
            .Add(GenreChoices.Name(nameof(TrackRole.Melody)), new ByConvention(chance, chance, chance))
            .Add(GenreChoices.Name(nameof(TrackRole.CounterMelody)), new ByConvention(chance, chance, chance))));

    /// <summary>The melody's notes off their chord that the next, over the same chord, resolves to one of its notes a step away, and every note.</summary>
    private static (int Resolved, int Notes) Measure(IEnumerable<CorpusSong> songs)
    {
        var (resolved, all) = (0, 0);
        foreach (var song in songs)
        {
            var notes = song.Song.Notes![song.Song.Track(TrackRole.Melody)].Where(x => x.Value.Pitches.Length == 1).ToArray();
            all += notes.Length;
            resolved += notes.Zip(notes.Skip(1)).Count(x =>
            {
                var chord = Realizer.GetChordNotes(x.First.Value.State).Classes;
                var (from, to) = (x.First.Value.Pitches[0], x.Second.Value.Pitches[0]);
                return !chord.Contains(from.Mod(12)) && chord.Contains(to.Mod(12)) && Math.Abs(to - from) is 1 or 2
                       && Realizer.GetChordNotes(x.Second.Value.State).Classes.SetEquals(chord);
            });
        }

        return (resolved, all);
    }

    [Test]
    public async Task ALeaningNote_TheNextResolvesItByStep_MoreThanWithNone()
    {
        var leaning = Measure(TestCorpus.InParallel(Enumerable.Range(0, 64), seed => TestCorpus.Get(seed, new SongOverrides(Genre: Leaning(1)))));
        var none = Measure(TestCorpus.InParallel(Enumerable.Range(0, 64), seed => TestCorpus.Get(seed, new SongOverrides(Genre: Leaning(0)))));
        var own = Measure(TestCorpus.Range(64));
        // against the mechanism breaking (measured: 29% to 30% of the notes leaning every time against 7% never, the songs' own 8% to 9%, at offsets 0 and a million)
        Console.WriteLine($"the melody's notes resolved by step from off the chord: {leaning.Resolved} of {leaning.Notes} leaning every time, {own.Resolved} of {own.Notes} the songs' own, {none.Resolved} of {none.Notes} never");
        await Assert.That(leaning.Resolved).IsGreaterThan(none.Resolved + leaning.Notes / 50);
    }

    /// <summary>The plainest melodies never lean on a tension, nor call for the band to answer.</summary>
    [Test]
    public async Task ThePlainestMelodies_NeitherLeanNorCall()
    {
        foreach (var song in TestCorpus.Streamed(Enumerable.Range(0, 16), seed => TestCorpus.Get(seed, new SongOverrides(Facets: TestCorpus.FacetsOf((Facet.Melody, 0))))))
        {
            foreach (var role in new[] { TrackRole.Melody, TrackRole.CounterMelody })
                if (song.Song.Notes!.TryGetValue(song.Track(role), out var notes))
                    await Assert.That(notes.All(x => x.Value.State.GetStateValue(CompositionStateKinds.LineAppoggiatura) == 0)).IsTrue().Because($"seed {song.Seed}, {role}");
            await Assert.That(song.Trace.Where(x => x.Point == TracePoints.CallAndResponse).All(x => !(bool)x.Value!)).IsTrue().Because($"seed {song.Seed}");
        }
    }
}

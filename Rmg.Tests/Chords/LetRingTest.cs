using System.Collections.Immutable;
using Rmg.Core.Composition;
using Rmg.Core.Probabilities;
using Rmg.Core.Songs;

namespace Rmg.Tests.Chords;

/// <summary>A part's broken chords let ring (<see cref="Articulations.LetRingChance" />): each note sounding on to its chord's change, its seconds ringing together.</summary>
public sealed class LetRingTest
{
    private static Genre Ringing(double chance) => new(ImmutableDictionary<string, ImmutableDictionary<string, ByConvention>>.Empty
        .Add(GenreChoices.Arpeggio.Name, ImmutableDictionary<string, ByConvention>.Empty.Add(GenreChoices.Name(nameof(ArpeggioPattern.Up)), new ByConvention(1, 1, 1)))
        .Add(GenreChoices.Articulation.Name, ImmutableDictionary<string, ByConvention>.Empty.Add("letRing", new ByConvention(chance, chance, chance)))
        .Add(GenreChoices.Instruments[TrackRole.Chords].Name, ImmutableDictionary<string, ByConvention>.Empty.Add(GenreChoices.Name("Acoustic Guitar (steel)"), new ByConvention(1, 1, 1))));

    /// <summary>The broken chords' notes sounding over the next, of the songs given, and every one.</summary>
    private static (int Ringing, int Notes) Measure(IEnumerable<CorpusSong> songs)
    {
        var (ringing, all) = (0, 0);
        foreach (var song in songs)
        {
            var notes = song.Song.Notes![song.Song.Track(TrackRole.Chords)].Where(x => x.Value.State.GetStateValue(CompositionStateKinds.Arpeggio).Value != 0).ToArray();
            all += notes.Length;
            ringing += notes.Zip(notes.Skip(1)).Count(x => x.First.Position + x.First.Value.Duration > x.Second.Position + 1e-6);
        }

        return (ringing, all);
    }

    [Test]
    public async Task ABrokenChordLetRing_SoundsOnToItsChange_ABarAtMost_NeverOverItsOwnPitchStruckAgain_OrUnder8ths()
    {
        // the same 64 songs with ringing and without, for the rule and the calibration both
        var songs = TestCorpus.InParallel(Enumerable.Range(0, 64), seed => TestCorpus.Get(seed, new SongOverrides(Genre: Ringing(1)))).ToArray();
        var unrung = TestCorpus.InParallel(Enumerable.Range(0, 64), seed => TestCorpus.Get(seed, new SongOverrides(Genre: Ringing(0)))).ToArray();
        foreach (var (song, without) in songs.Zip(unrung))
        {
            var changes = song.ChordChanges;
            var track = song.Song.Track(TrackRole.Chords);
            var notes = song.Song.Notes![track].ToArray();
            // but the song's opening chord where a silent start holds it from the first beat into where it was, every other
            // part coming in a bar or more in (SilentStart)
            var opened = notes is [{ Position: < 1e-9 }, ..] && song.Song.Notes.Where(x => x.Key != track)
                .All(x => x.Value.Count == 0 || x.Value[0].Position >= song.Map.MeterAt(0).BarDuration - 1e-9);
            var before = without.Song.Notes![without.Song.Track(TrackRole.Chords)].ToArray();
            await Assert.That(notes.Select(x => x.Position)).IsEquivalentTo(before.Select(x => x.Position), CollectionOrdering.Matching).Because($"seed {song.Seed}");
            for (var i = 0; i + 1 < notes.Length; i++)
            {
                // a note let ring, longer than it was
                var (note, next) = (notes[i], notes[i + 1]);
                if (note.Value.Duration <= before[i].Value.Duration + 1e-9 || i == 0 && opened)
                    continue;
                var because = $"seed {song.Seed} at {note.Position}";
                // rung on: its figure an 8th or slower, to its chord's change, a bar at most, its own pitch not struck again
                await Assert.That(next.Position - note.Position).IsGreaterThanOrEqualTo(Articulations.RingsFrom - 1e-9).Because(because);
                await Assert.That(note.Position + note.Value.Duration).IsLessThanOrEqualTo(changes.FirstOrDefault(x => x > note.Position + 1e-9, double.MaxValue) + 1e-9).Because(because);
                await Assert.That(note.Value.Duration).IsLessThanOrEqualTo(song.Map.MeterAt(note.Position).BarDuration + 1e-9).Because(because);
                await Assert.That(notes.Skip(i + 1).Any(x => x.Position < note.Position + note.Value.Duration - 1e-9 && x.Value.Pitches.Contains(note.Value.Pitches[0]))).IsFalse().Because(because);
            }
        }

        // against the same songs with no ringing, over 64 (measured: 63% against 42% over 64, 64% to 73% against 48% to 54% over 16 at offsets 0 and a million)
        var (rung, all) = Measure(songs);
        var plain = Measure(unrung);
        Console.WriteLine($"{rung} of {all} broken chords' notes ringing over the next, against {plain.Ringing} of {plain.Notes} with none rung");
        await Assert.That(rung).IsGreaterThan(plain.Ringing + all / 10);
    }
}

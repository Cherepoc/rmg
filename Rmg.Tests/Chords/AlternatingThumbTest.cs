using Rmg.Core;
using Rmg.Core.Composition;
using Rmg.Core.Events;
using Rmg.Core.Songs;
using Rmg.Tests.Genres;

namespace Rmg.Tests.Chords;

/// <summary>The alternating thumb: the root and the fifth on the beats by turns, the fingers above them off the beats.</summary>
public sealed class AlternatingThumbTest
{
    [Test]
    public async Task TheThumb_AlternatesRootAndFifthOnTheBeats_TheFingersPickAboveOffThem()
    {
        // a C major chord voiced C3 G3 C4 E4
        int[] voiced = [48, 55, 60, 64];
        int At(int place, double position, bool onBeat) => Arpeggios.Pick(ArpeggioPattern.AlternatingThumb, [..voiced], place, position, onBeat, (int)position, 0, 0, position % 4);

        await Assert.That(At(0, 0, true)).IsEqualTo(48);
        await Assert.That(At(2, 1, true)).IsEqualTo(55);
        await Assert.That(At(4, 2, true)).IsEqualTo(48);
        await Assert.That(At(6, 3, true)).IsEqualTo(55);
        // off the beats the notes above the fifth, by turns
        int[] fingers = [At(1, 0.5, false), At(3, 1.5, false), At(5, 2.5, false), At(7, 3.5, false)];
        await Assert.That(fingers.All(x => x > 55)).IsTrue();
        await Assert.That(fingers.Distinct().Count()).IsEqualTo(2);
    }

    /// <summary>
    ///     In a song, the thumb strikes the same note on every bar's first pulse under one chord, whatever the meter, as it
    ///     alternates by its bar's pulses, not the song's quarters: a 6/8 bar starting on the root every bar, not every other.
    /// </summary>
    [Test]
    public async Task TheThumb_StrikesEveryBarsFirstPulseAlike_UnderOneChord_InEveryMeter()
    {
        var (groups, triple) = (0, 0);
        // Folk's songs, and some of them given 6/8, so that a meter of dotted pulses is met whatever the songs draw
        var folk = GenrePresets.Genre("folk");
        foreach (var song in TestCorpus.InParallel(Enumerable.Range(0, 64), seed => TestCorpus.Get(seed, new SongOverrides(Genre: folk)))
                     .Concat(TestCorpus.InParallel(Enumerable.Range(0, 16), seed => TestCorpus.Get(seed, new SongOverrides(Genre: folk, Meter: Meter.SixEight)))))
        {
            var changes = song.ChordChanges;
            foreach (var (track, notes) in song.Song.Notes!.Where(x => song.Song.TrackDefinitions[x.Key].Role is TrackRole.Chords or TrackRole.Rhythm))
            foreach (var chord in notes.Where(x => (ArpeggioPattern)x.Value.State.GetStateValue(CompositionStateKinds.Arpeggio) == ArpeggioPattern.AlternatingThumb
                                                   && x.Value.State.GetStateValue(CompositionStateKinds.Colour) == 0 && x.Value.Pitches.Length == 1
                                                   // but a solo's line the part plays, which keeps its part's state
                                                   && x.Value.State.GetStateValue(CompositionStateKinds.LineSolo) == 0
                                                   && song.Map.PlaceOf(x.Position) is { OnPulse: true, Pulse: 0 })
                         .GroupBy(x => Array.FindLastIndex(changes, c => c <= x.Position + 1e-9)).Where(x => x.Count() > 1))
            {
                groups++;
                triple += song.Map.PlaceOf(chord.First().Position).Meter.PulseSixteenths % 3 == 0 ? 1 : 0;
                await Assert.That(chord.Select(x => x.Value.Pitches[0] % 12).Distinct().Count()).IsEqualTo(1).Because($"seed {song.Seed} at {chord.First().Position}, track {track}");
            }
        }

        Console.WriteLine($"{groups} chords held over bars, {triple} in a meter of dotted pulses");
        await Assert.That(groups).IsGreaterThan(20);
        await Assert.That(triple).IsGreaterThan(0);
    }
}

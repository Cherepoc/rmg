using Rmg.Core.Composition;
using Rmg.Core.Songs;

namespace Rmg.Tests.Meters;

/// <summary>12/8 (<see cref="Meter.TwelveEight" />): four dotted quarters of three 8ths, a slow blues' triplets.</summary>
public sealed class TwelveEightTest
{
    [Test]
    public async Task ASongIn12_8_PlaysBarsOfFourGroupsOfThree_ItsBrokenChordsOnItsEighths()
    {
        var (broken, offGrid) = (0, 0);
        foreach (var song in TestCorpus.InParallel(Enumerable.Range(0, 64), seed => TestCorpus.Get(seed, Meter.TwelveEight)))
        {
            await Assert.That(Meter.NameOf(song.Map.Meter)).IsEqualTo("12/8");
            await Assert.That(song.Map.Meter.BarDuration).IsEqualTo(6);
            var notes = song.Song.Notes!.GetValueOrDefault(song.Song.Track(TrackRole.Chords))?.Where(x => x.Value.State.GetStateValue(CompositionStateKinds.Arpeggio) != 0
                && x.Value.State.GetStateValue(CompositionStateKinds.LineSolo) == 0 && song.Map.MeterAt(x.Position) == song.Map.Meter).ToArray() ?? [];
            broken += notes.Length;
            // off the 8ths of its bar, counted from the bar's start
            offGrid += notes.Count(x => song.Map.PlaceOf(x.Position).Beat * 2 is var eighths && Math.Abs(eighths - Math.Round(eighths)) > 1e-6);
        }

        Console.WriteLine($"{broken} broken chords' notes in 12/8, {offGrid} off its 8ths, {offGrid / (double)broken:P0}");
        await Assert.That(broken).IsGreaterThan(100);
        // but a figure of 16ths now and then, a calibration (measured over 64 songs: 15%, 27%, 14% and 15% at offsets 0,
        // 725000000, 816000000 and 1000000)
        await Assert.That(offGrid).IsLessThan(broken * 2 / 5);
    }
}

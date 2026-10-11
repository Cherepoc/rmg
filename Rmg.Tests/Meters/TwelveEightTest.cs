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
        foreach (var song in TestCorpus.InParallel(Enumerable.Range(0, 32), seed => TestCorpus.Get(seed, Meter.TwelveEight)))
        {
            await Assert.That(Meter.NameOf(song.Map.Meter)).IsEqualTo("12/8");
            await Assert.That(song.Map.Meter.BarDuration).IsEqualTo(6);
            var notes = song.Song.Notes!.GetValueOrDefault(song.Song.Track(TrackRole.Chords))?.Where(x => x.Value.State.GetStateValue(CompositionStateKinds.Arpeggio) != 0
                && x.Value.State.GetStateValue(CompositionStateKinds.LineSolo) == 0 && song.Map.MeterAt(x.Position) == song.Map.Meter).ToArray() ?? [];
            broken += notes.Length;
            offGrid += notes.Count(x => Math.Abs(x.Position * 2 - Math.Round(x.Position * 2)) > 1e-6);
        }

        Console.WriteLine($"{broken} broken chords' notes in 12/8, {offGrid} off its 8ths");
        await Assert.That(broken).IsGreaterThan(100);
        // but a figure of 16ths now and then (measured: 16% at offset 0)
        await Assert.That(offGrid).IsLessThan(broken / 4);
    }
}

using System.Collections.Immutable;
using Rmg.Core.Composition;
using Rmg.Core.Probabilities;

namespace Rmg.Tests.Forms;

public sealed class LiftTest
{
    [Test]
    public async Task AGenresLongLift_SwellsFromBeforeTheSectionsLastBar_TheNotesOtherwiseAsTheSongsAre()
    {
        var genre = new Genre(ImmutableDictionary<string, ImmutableDictionary<string, ByConvention>>.Empty
            .Add("dynamics", ImmutableDictionary<string, ByConvention>.Empty.Add("lift", new(1, 1, 1))));
        var (earlier, louder) = (0, 0);
        foreach (var seed in Enumerable.Range(0, 16))
        {
            var (own, longer) = (TestCorpus.Get(seed), TestCorpus.Get(seed, new SongOverrides(Genre: genre)));
            // each change of section, the bar before it, a short last bar's, and the full bars of the section it ends
            var starts = own.Map.Sections.Skip(1).Select((x, i) => (x.Start, Last: own.Map.BarBefore(x.Start), own.Map.Sections[i].Meter.BarDuration)).ToArray();
            foreach (var (track, notes) in own.Song.Notes!)
            {
                var other = longer.Song.Notes![track];
                await Assert.That(other.Count).IsEqualTo(notes.Count);
                foreach (var (a, b) in notes.Zip(other))
                {
                    await Assert.That(b.Position).IsEqualTo(a.Position);
                    if (Math.Abs(a.Value.Velocity - b.Value.Velocity) > 1e-9
                        && starts.Any(x => a.Position < x.Start - x.Last - 1e-9 && a.Position >= x.Start - x.Last - 3 * x.BarDuration - 1e-9))
                    {
                        earlier++;
                        louder += b.Value.Velocity > a.Value.Velocity ? 1 : 0;
                    }
                }
            }
        }

        // swelling: the notes it reaches before the last bar louder, all but the few a stage after it plays as loud as
        // another note there
        Console.WriteLine($"{louder} of {earlier} notes before the last bar louder");
        await Assert.That(earlier).IsGreaterThan(0);
        await Assert.That((double)louder).IsGreaterThan(earlier * 0.95);
    }
}

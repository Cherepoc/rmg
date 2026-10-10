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

    /// <summary>
    ///     The band lifts into a louder section at a change of section, whether its drums mark the line or not: every lift
    ///     at a section's start, a hook intro's end among them, rising, and drumless songs lifting as the others do.
    /// </summary>
    [Test]
    public async Task TheBand_LiftsIntoALouderSection_AtEveryChangeOfSection_DrumsOrNone()
    {
        var (lifts, drumless) = (0, 0);
        foreach (var song in TestCorpus.InParallel(Enumerable.Range(0, 64), seed => TestCorpus.Get(seed, new SongOverrides(Parts: ImmutableDictionary<Rmg.Core.Songs.TrackRole, bool>.Empty.Add(Rmg.Core.Songs.TrackRole.Drum, seed % 2 == 0)))))
        {
            var starts = song.Map.Sections.Select(x => Math.Round(x.Start, 6)).ToHashSet();
            foreach (var (position, lift) in song.Trace.Where(x => x.Point == TracePoints.Lift).Select(x => ((double, double))x.Value!))
            {
                lifts++;
                drumless += song.PlaysDrums ? 0 : 1;
                await Assert.That(starts.Contains(Math.Round(position, 6))).IsTrue().Because($"seed {song.Seed} at {position}");
                await Assert.That(lift).IsGreaterThan(0);
            }
        }

        Console.WriteLine($"{lifts} lifts, {drumless} in songs without drums");
        await Assert.That(lifts).IsGreaterThan(50);
        await Assert.That(drumless).IsGreaterThan(lifts / 5);
    }
}

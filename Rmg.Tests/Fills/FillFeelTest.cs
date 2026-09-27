using Rmg.Core.Composition;
using Rmg.Core.Events;
using Rmg.Core.Probabilities;

namespace Rmg.Tests.Fills;

public sealed class FillFeelTest
{
    private static int IndexOf(double periodValue) =>
        Enumerable.Range(-RhythmPeriod.MaxPrimeIndex, 2 * RhythmPeriod.MaxPrimeIndex + 1)
            .Single(x => Math.Abs(x.ToRhythmPeriodValue() - periodValue) < 1e-9);

    [Test]
    public async Task ToTuplet_IsThePrime_WhereItDividesTheBeat()
    {
        await Assert.That(0.ToTuplet()).IsEqualTo(1);
        // triplets divide the beat by three, dotted 8ths lengthen it and stay on the 16ths
        await Assert.That(IndexOf(4.0 / 3).ToTuplet()).IsEqualTo(3);
        await Assert.That(IndexOf(3.0 / 4).ToTuplet()).IsEqualTo(1);
        await Assert.That(IndexOf(4.0 / 5).ToTuplet()).IsEqualTo(5);
        await Assert.That(IndexOf(5.0 / 4).ToTuplet()).IsEqualTo(1);
    }

    private static ResolvedRhythm Rhythm(int tuplet)
    {
        var index = Enumerable.Range(-RhythmPeriod.MaxPrimeIndex, 2 * RhythmPeriod.MaxPrimeIndex + 1).First(x => x.ToTuplet() == tuplet);
        return ResolvedRhythm.Default with { PrimeIndex = index, PeriodValue = ResolvedRhythm.Default.PeriodValue * index.ToRhythmPeriodValue() };
    }

    [Test]
    public async Task TupletSections_PlayTheirFillsInTheirTuplet()
    {
        var context = new GenerationContext(1);
        var tracks = SongTracks.Create(context, new RhythmicUnconventionality(0.5));
        var song = TrackEventStateTimelineMap.Create<StateMap>(8 * 32);
        var sections = Enumerable.Range(0, 8).Select(x => new FillSection(x, 32, new RhythmicUnconventionality(0.5), Rhythm(3))).ToArray();
        using var trace = StateTrace.Start();

        new FillGenerator(context, tracks, new RhythmicUnconventionality(0.5)).Generate(song, FillGenerator.GetSectionLines(sections));

        var decisions = trace.Entries.Where(x => x.Point == "Fill decision").Select(x => x.Phrase!).ToArray();
        var hits = trace.Entries.Where(x => x.Point == "Fill" && x.Phrase != "Landing").ToArray();
        await Assert.That(decisions.Length).IsEqualTo(15);
        await Assert.That(decisions.Where(x => !x.StartsWith("None")).All(x => x.Contains("in 3s"))).IsTrue();
        // triplet 8ths and sextuplets, a sixth of a beat apart at the finest
        await Assert.That(hits.Length).IsGreaterThan(0);
        await Assert.That(hits.All(x => Math.Abs(x.Position * 6 - Math.Round(x.Position * 6)) < 1e-6)).IsTrue();
    }
}

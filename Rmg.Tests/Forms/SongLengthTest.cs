using Rmg.Core.Composition;

namespace Rmg.Tests.Forms;

public sealed class SongLengthTest
{
    [Test]
    [Arguments(120.0)]
    [Arguments(300.0)]
    public async Task ASongGivenALength_ComesWithinAFifthOfItMostly(double target)
    {
        var errors = TestCorpus.InParallel(Enumerable.Range(0, 64), seed => Math.Abs(TestCorpus.Get(seed, new SongOverrides(Length: target)).Rendered.Seconds / target - 1));
        Console.WriteLine($"{target} s: {errors.Count(x => x <= 0.2)} of 64 within a fifth");

        // 77% within 15% of two minutes and 92% of five over 128 songs (SongLengthReportTest)
        // seen 54 to 61 of 64 over three corpora, at 120 s and 300 s
        await Assert.That(errors.Count(x => x <= 0.2)).IsGreaterThanOrEqualTo(40);
    }

    [Test]
    public async Task ALongerLength_PlaysItsSectionsLonger_AmongThoseTheFormsReach()
    {
        int Plays(CorpusSong song) => song.Trace.Where(x => x.Point == TracePoints.SectionLength).Sum(x => (int)x.Value!);
        double Average(double target) => TestCorpus.InParallel(Enumerable.Range(0, 64), seed => TestCorpus.Get(seed, new SongOverrides(Length: target)))
            .Average(x => Plays(x) / (double)x.Trace.Count(t => t.Point == TracePoints.SectionLength));

        // among the lengths the forms reach: 1.43 plays a section at a minute, 2.81 at three and 3.19 at five; ten, mostly
        // in free forms, as many more sections as longer ones, 2.77
        var (one, three, five) = (Average(60), Average(180), Average(300));
        await Assert.That(one).IsLessThan(three);
        await Assert.That(three).IsLessThan(five);
    }
}

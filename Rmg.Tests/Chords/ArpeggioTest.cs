using Rmg.Core.Composition;
using Rmg.Core.Probabilities;

namespace Rmg.Tests.Chords;

public sealed class ArpeggioTest
{
    private static readonly System.Collections.Immutable.ImmutableArray<int> Chord = [60, 64, 67, 72];

    private static int[] Play(ArpeggioPattern pattern, int notes) => [..Enumerable.Range(0, notes).Select(place => Arpeggios.Pick(pattern, Chord, place, place * 0.5))];

    [Test]
    public async Task APattern_PlaysTheChordsNotes_InItsOrder()
    {
        await Assert.That(Play(ArpeggioPattern.Up, 6)).IsEquivalentTo(new[] { 60, 64, 67, 72, 60, 64 });
        await Assert.That(Play(ArpeggioPattern.Down, 5)).IsEquivalentTo(new[] { 72, 67, 64, 60, 72 });
        await Assert.That(Play(ArpeggioPattern.UpDown, 8)).IsEquivalentTo(new[] { 60, 64, 67, 72, 67, 64, 60, 64 });
        await Assert.That(Play(ArpeggioPattern.Alberti, 4)).IsEquivalentTo(new[] { 60, 72, 67, 72 });
        await Assert.That(Play(ArpeggioPattern.RootFifth, 4)).IsEquivalentTo(new[] { 60, 67, 60, 67 });
        await Assert.That(Play(ArpeggioPattern.UpTwoOctaves, 8)).IsEquivalentTo(new[] { 60, 64, 67, 72, 72, 76, 79, 84 });
    }

    [Test]
    public async Task ThePlainestSections_BreakTheirChordsTheCommonWays_OrNot()
    {
        var context = new GenerationContext(3);
        var plain = Enumerable.Range(0, 2000).Select(_ => Arpeggios.Draw(context, 0)).ToHashSet();

        await Assert.That(plain.SetEquals([ArpeggioPattern.None, ArpeggioPattern.Up, ArpeggioPattern.UpDown, ArpeggioPattern.Alberti])).IsTrue();
        await Assert.That(Enumerable.Range(0, 4000).Select(_ => Arpeggios.Draw(context, 1)).Distinct().Count()).IsEqualTo(Enum.GetValues<ArpeggioPattern>().Length);
    }

    [Test]
    [Explicit]
    public async Task Report()
    {
        var sections = TestCorpus.Range(256).SelectMany(song => song.Trace.Where(x => x.Point == TracePoints.Arpeggio)
            .Select(x => (Pattern: (ArpeggioPattern)x.Value!, Chords: song.Trace.First(t => t.Point == TracePoints.SectionUnconventionality && t.Section == x.Section).Value is Unconventionality u ? u[Facet.Chords] : 0))).ToArray();
        foreach (var band in sections.GroupBy(x => Math.Clamp((int)(x.Chords * 5), 0, 4)).OrderBy(x => x.Key))
            Console.WriteLine($"  chords {band.Key}/5, {band.Count()} sections: broken {band.Count(x => x.Pattern != ArpeggioPattern.None) / (double)band.Count():P0}; {string.Join(", ", band.Where(x => x.Pattern != ArpeggioPattern.None).GroupBy(x => x.Pattern).OrderByDescending(x => x.Count()).Select(x => $"{x.Key} {x.Count()}"))}");
        await Task.CompletedTask;
    }
}

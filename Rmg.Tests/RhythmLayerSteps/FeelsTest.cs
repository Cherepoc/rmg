using Rmg.Core.Composition;
using Rmg.Core.Probabilities;

namespace Rmg.Tests.RhythmLayerSteps;

public sealed class FeelsTest
{
    private const int DrawCount = 20000;

    [Test]
    public async Task ThePlainestSongs_AreStraight_AndNeverChangeTheirFeel()
    {
        var context = new GenerationContext(1);
        var feels = Enumerable.Range(0, DrawCount).Select(_ => Feels.DrawSong(context, 0, Genre.None)).ToArray();

        await Assert.That(feels.All(x => x == Feels.Straight)).IsTrue();
        foreach (var chance in new[] { Feels.SectionChange, Feels.BarChange, Feels.FillChange })
            await Assert.That(Enumerable.Range(0, 1000).All(_ => Feels.DrawChange(context, Feels.Straight, chance, 0, Genre.None, GenreChoices.Feel) is null)).IsTrue();
    }

    [Test]
    public async Task TheWildestSongs_PlayEveryFeelButStraight_AsLikely_AndChangeItEverywhere()
    {
        var context = new GenerationContext(1);
        var feels = Enumerable.Range(0, DrawCount).Select(_ => Feels.DrawSong(context, 1, Genre.None)).ToArray();

        await Assert.That(feels.All(x => x != Feels.Straight)).IsTrue();
        // each by its wild weight, as likely as the others
        var others = Feels.Weights.Where(x => x.Feel != Feels.Straight).ToArray();
        await Assert.That(others.Select(x => x.Weight.Wild).Distinct().Count()).IsEqualTo(1);
        foreach (var (feel, _) in others)
            await Assert.That(feels.Count(x => x == feel) / (double)DrawCount).IsEqualTo(1.0 / others.Length).Within(0.01);
        var changes = Enumerable.Range(0, 1000).Select(_ => Feels.DrawChange(context, 1, Feels.BarChange, 1, Genre.None, GenreChoices.Feel)).ToArray();
        await Assert.That(changes.All(x => x is { } changed && changed != 1 && changed != Feels.Straight)).IsTrue();
    }

    [Test]
    public async Task AMiddlingChange_LeavesTheFeelItComesTo_MostOftenForStraightTime()
    {
        var context = new GenerationContext(1);
        var changes = Enumerable.Range(0, DrawCount).Select(_ => Feels.DrawChange(context, 1, Feels.SectionChange, 0.5, Genre.None, GenreChoices.Feel)).ToArray();
        var changed = changes.OfType<int>().ToArray();

        await Assert.That(changed.Length / (double)DrawCount).IsEqualTo(Feels.SectionChange.Tuned).Within(0.01);
        await Assert.That(changed.All(x => x != 1)).IsTrue();
        await Assert.That(changed.Count(x => x == Feels.Straight) / (double)changed.Length).IsGreaterThan(0.9);
    }
}

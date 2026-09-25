using Rmg.Core.Composition;
using Rmg.Core.Probabilities;

namespace Rmg.Tests.Chords;

public sealed class ChordShapesTest
{
    [Test]
    public async Task EveryRank_HasShapes()
    {
        var ranks = ChordShapes.All.Select(x => x.Rank).Distinct().Order().ToArray();

        await Assert.That(ranks).IsEquivalentTo(Enumerable.Range(0, ChordShapes.MaxRank + 1).ToArray());
    }

    [Test]
    public async Task EveryShape_StartsAtItsRoot_AndRises()
    {
        foreach (var shape in ChordShapes.All)
        {
            await Assert.That(shape.Targets[0]).IsEqualTo(0).Because(shape.Name);
            await Assert.That(shape.Targets.Zip(shape.Targets.Skip(1)).All(x => x.First < x.Second))
                .IsTrue()
                .Because(shape.Name);
            await Assert.That(shape.Weight).IsGreaterThan(0).Because(shape.Name);
        }
    }

    [Test]
    public async Task Pick_ResultsIn_ShapesOfTheRank_TheHeavierMoreOften()
    {
        var context = new GenerationContext(0);
        var picks = Enumerable.Range(0, 10000).Select(_ => ChordShapes.Pick(context, 1)).ToArray();

        await Assert.That(picks.All(x => x.Rank == 1)).IsTrue();
        // the seventh weighs 1 and the sixth 0.5
        await Assert.That(picks.Count(x => x.Name == "Seventh")).IsGreaterThan(picks.Count(x => x.Name == "Sixth") * 3 / 2);
    }
}

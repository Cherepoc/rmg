using Rmg.Core.Composition;
using Rmg.Core.Probabilities;

namespace Rmg.Tests.SongStructures;

public sealed class SectionBrushTest
{
    public static IEnumerable<(int Distinct, int Length)> Shapes()
    {
        yield return (1, 1);
        yield return (2, 2);
        yield return (2, 3);
        yield return (2, 4);
        yield return (3, 3);
        yield return (3, 4);
    }

    [Test]
    [MethodDataSource(nameof(Shapes))]
    public async Task AllBrushes_NeverRepeatAdjacent_UseAllDistinct_AndStayInRange(int distinct, int length)
    {
        foreach (var brush in SectionBrush.All)
        {
            for (var seed = 0; seed < 200; seed++)
            {
                var result = brush.Paint(distinct, length, new GenerationContext(seed));

                await Assert.That(result.Length).IsEqualTo(length);
                await Assert.That(result.Distinct().Count()).IsEqualTo(distinct);
                await Assert.That(result.All(x => x >= 0 && x < distinct)).IsTrue();
                for (var i = 1; i < result.Length; i++)
                    await Assert.That(result[i]).IsNotEqualTo(result[i - 1]);
            }
        }
    }

    [Test]
    public async Task Alternation_Alternates()
    {
        var result = SectionBrush.Alternation.Paint(2, 4, new GenerationContext(1));

        await Assert.That(result.AsEnumerable()).IsEquivalentTo([0, 1, 0, 1]);
    }

    [Test]
    public async Task PingPong_BouncesBetweenEnds()
    {
        var result = SectionBrush.PingPong.Paint(3, 4, new GenerationContext(1));

        await Assert.That(result.AsEnumerable()).IsEquivalentTo([0, 1, 2, 1]);
    }

    [Test]
    public async Task Random_SameSeed_ResultsIn_SameSequence()
    {
        var first = SectionBrush.Random.Paint(3, 4, new GenerationContext(9));
        var second = SectionBrush.Random.Paint(3, 4, new GenerationContext(9));

        await Assert.That(first.AsEnumerable()).IsEquivalentTo(second.AsEnumerable());
    }

    [Test]
    public async Task SingleDistinctSection_LongerThanOne_Throws()
    {
        await Assert.That(() => SectionBrush.Alternation.Paint(1, 2, new GenerationContext(1)))
            .Throws<ArgumentOutOfRangeException>();
    }
}

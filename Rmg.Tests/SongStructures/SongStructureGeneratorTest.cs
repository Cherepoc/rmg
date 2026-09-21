using Rmg.Core.Composition;
using Rmg.Core.Probabilities;

namespace Rmg.Tests.SongStructures;

public sealed class SongStructureGeneratorTest
{
    [Test]
    public async Task Parts_RespectLimits_AndNeverRepeatAdjacentSection()
    {
        for (var seed = 0; seed < 500; seed++)
        {
            var parts = SongStructureGenerator.Generate(new GenerationContext(seed));

            await Assert.That(parts.Length).IsBetween(4, 8);
            foreach (var part in parts)
            {
                await Assert.That(part.SectionIds.Length).IsBetween(1, 4);
                await Assert.That(part.SectionIds.Distinct().Count()).IsBetween(1, 3);
                for (var i = 1; i < part.SectionIds.Length; i++)
                    await Assert.That(part.SectionIds[i]).IsNotEqualTo(part.SectionIds[i - 1]);
            }
        }
    }

    [Test]
    public async Task SectionIds_HaveNoGaps()
    {
        for (var seed = 0; seed < 200; seed++)
        {
            var ids = SongStructureGenerator.Generate(new GenerationContext(seed))
                .SelectMany(x => x.SectionIds)
                .Distinct()
                .Order()
                .ToArray();

            await Assert.That(ids).IsEquivalentTo(Enumerable.Range(0, ids.Length).ToArray());
        }
    }

    [Test]
    public async Task SectionsAreSometimesSharedAcrossParts()
    {
        var shared = Enumerable.Range(0, 200)
            .Select(seed => SongStructureGenerator.Generate(new GenerationContext(seed)))
            .Count(parts =>
                {
                    var all = parts.SelectMany(p => p.SectionIds.Distinct()).ToList();
                    return all.Count != all.Distinct().Count();
                }
            );

        await Assert.That(shared).IsGreaterThan(0);
    }

    [Test]
    public async Task SameSeed_ResultsIn_SameStructure()
    {
        var first = SongStructureGenerator.Generate(new GenerationContext(5));
        var second = SongStructureGenerator.Generate(new GenerationContext(5));

        await Assert.That(first.SelectMany(x => x.SectionIds).ToArray())
            .IsEquivalentTo(second.SelectMany(x => x.SectionIds).ToArray());
    }
}

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
            var parts = SongStructureGenerator.Generate(new GenerationContext((ulong)seed), null, 0.5);

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
    public async Task Sections_NeverRepeat_AcrossPartBoundaries()
    {
        for (var seed = 0; seed < 2000; seed++)
        {
            var sectionIds = SongStructureGenerator.Generate(new GenerationContext((ulong)seed), null, 0.5)
                .SelectMany(x => x.SectionIds)
                .ToArray();

            for (var i = 1; i < sectionIds.Length; i++)
                await Assert.That(sectionIds[i]).IsNotEqualTo(sectionIds[i - 1]);
        }
    }

    [Test]
    public async Task SectionIds_HaveNoGaps()
    {
        for (var seed = 0; seed < 200; seed++)
        {
            var ids = SongStructureGenerator.Generate(new GenerationContext((ulong)seed), null, 0.5)
                .SelectMany(x => x.SectionIds)
                .Distinct()
                .Order()
                .ToArray();

            await Assert.That(ids).IsEquivalentTo(Enumerable.Range(0, ids.Length).ToArray(), CollectionOrdering.Matching);
        }
    }

    [Test]
    public async Task SectionsAreSometimesSharedAcrossParts()
    {
        var shared = Enumerable.Range(0, 200)
            .Select(seed => SongStructureGenerator.Generate(new GenerationContext((ulong)seed), null, 0.5))
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
        var first = SongStructureGenerator.Generate(new GenerationContext(5), null, 0.5);
        var second = SongStructureGenerator.Generate(new GenerationContext(5), null, 0.5);

        await Assert.That(first.SelectMany(x => x.SectionIds).ToArray())
            .IsEquivalentTo(second.SelectMany(x => x.SectionIds).ToArray(), CollectionOrdering.Matching);
    }

    /// <summary>
    ///     A free form of a length given keeps of its parts, drawn to the most there may be, the first as many as come
    ///     nearest the length, by their sections' plays.
    /// </summary>
    [Test]
    public async Task ALengthGiven_KeepsTheFirstParts_ThatComeNearestIt()
    {
        // an appearance of ten seconds, whatever its role
        static SongLength Length(double target) => new(target, (_, _) => 10);

        for (var seed = 0; seed < 50; seed++)
        {
            var all = SongStructureGenerator.Generate(new GenerationContext((ulong)seed), Length(1e9), 0.5);
            var kept = SongStructureGenerator.Generate(new GenerationContext((ulong)seed), Length(300), 0.5);
            double Distance(int count) => Math.Abs(Math.Log(all.Take(count).Sum(x => x.SectionIds.Length) * 10 / 300.0));

            await Assert.That(all.Length).IsEqualTo(SongStructureGenerator.MaxPartCountForLength);
            await Assert.That(kept.SelectMany(x => x.SectionIds).ToArray())
                .IsEquivalentTo(all.Take(kept.Length).SelectMany(x => x.SectionIds).ToArray(), CollectionOrdering.Matching);
            await Assert.That(Enumerable.Range(1, all.Length).All(n => Distance(n) >= Distance(kept.Length) - 1e-12)).IsTrue().Because($"seed {seed}");
        }
    }
}

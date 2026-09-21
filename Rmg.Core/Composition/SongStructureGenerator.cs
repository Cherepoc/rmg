using System.Collections.Immutable;
using Rmg.Core.Probabilities;

namespace Rmg.Core.Composition;

public static class SongStructureGenerator
{
    public const int MinPartCount = 4;
    public const int MaxPartCount = 8;
    public const int MinPartLength = 1;
    public const int MaxPartLength = 4;
    public const int MaxDistinctSectionsInPart = 3;

    private const double ReuseSectionProbability = 0.4;

    private static readonly Func<IGenerationContext, int> PartCountGenerator =
        Generators.Int(MinPartCount, MaxPartCount + 1);

    private static readonly Func<IGenerationContext, int> PartLengthGenerator =
        Generators.Int(MinPartLength, MaxPartLength + 1);

    private static readonly Func<IGenerationContext, SectionBrush> BrushGenerator =
        Generators.ItemSelector(SectionBrush.All);

    public static ImmutableArray<SongPart> Generate(IGenerationContext context)
    {
        var partCount = PartCountGenerator(context);
        var parts = ImmutableArray.CreateBuilder<SongPart>(partCount);
        var sectionCount = 0;

        for (var i = 0; i < partCount; i++)
        {
            var length = PartLengthGenerator(context);

            // no immediate repeats: length 1 has one section, otherwise at least two
            var minDistinct = length == 1 ? 1 : 2;
            var maxDistinct = Math.Min(length, MaxDistinctSectionsInPart);
            var distinctCount = context.GenerateInt(minDistinct, maxDistinct + 1);

            var partSectionIds = new List<int>(distinctCount);
            for (var j = 0; j < distinctCount; j++)
            {
                var reusable = Enumerable.Range(0, sectionCount)
                    .Where(x => !partSectionIds.Contains(x))
                    .ToArray();
                if (reusable.Length > 0 && context.TestProbability(ReuseSectionProbability))
                {
                    partSectionIds.Add(reusable[context.GenerateInt(0, reusable.Length)]);
                }
                else
                {
                    partSectionIds.Add(sectionCount);
                    sectionCount++;
                }
            }

            var brush = BrushGenerator(context);
            var sectionIds = brush.Paint(distinctCount, length, context)
                .Select(x => partSectionIds[x])
                .ToImmutableArray();
            parts.Add(new SongPart(brush, sectionIds));
        }

        return parts.ToImmutable();
    }
}

using System.Collections.Immutable;
using System.Diagnostics;
using Rmg.Core.Probabilities;

namespace Rmg.Core.Composition;

/// <summary>
///     A law of section changes inside a song part. A brush paints a sequence of the given length over
///     <c>distinctCount</c> sections (referenced by index) so that no section directly follows itself
///     and every distinct section is used at least once.
/// </summary>
[DebuggerDisplay("SectionBrush {Name}")]
public sealed class SectionBrush
{
    private readonly Func<int, int, IGenerationContext, ImmutableArray<int>> _paint;

    private SectionBrush(string name, Func<int, int, IGenerationContext, ImmutableArray<int>> paint)
    {
        Name = name;
        _paint = paint;
    }

    public string Name { get; }

    /// <summary>A B A B ... / A B C A ...</summary>
    public static SectionBrush Alternation { get; } = new(
        nameof(Alternation),
        (distinctCount, length, _) => Build(length, i => i % distinctCount)
    );

    /// <summary>A B C B A B ...</summary>
    public static SectionBrush PingPong { get; } = new(
        nameof(PingPong),
        (distinctCount, length, _) =>
        {
            if (distinctCount == 1)
                return Build(length, _ => 0);

            var cycle = 2 * (distinctCount - 1);
            return Build(
                length,
                i =>
                {
                    var p = i % cycle;
                    return p < distinctCount ? p : cycle - p;
                }
            );
        }
    );

    /// <summary>Random order without immediate repeats; all distinct sections are still used.</summary>
    public static SectionBrush Random { get; } = new(
        nameof(Random),
        (distinctCount, length, context) =>
        {
            // start with a random permutation so every section appears, then fill the rest freely
            var order = Enumerable.Range(0, distinctCount).ToArray();
            for (var i = order.Length - 1; i > 0; i--)
            {
                var j = context.GenerateInt(0, i + 1);
                (order[i], order[j]) = (order[j], order[i]);
            }

            var result = new int[length];
            for (var i = 0; i < length; i++)
            {
                if (i < distinctCount)
                {
                    result[i] = order[i];
                    continue;
                }

                // pick any section except the previous one
                var pick = context.GenerateInt(0, distinctCount - 1);
                result[i] = pick >= result[i - 1] ? pick + 1 : pick;
            }

            return [..result];
        }
    );

    public static ImmutableArray<SectionBrush> All { get; } = [Alternation, PingPong, Random];

    public ImmutableArray<int> Paint(int distinctCount, int length, IGenerationContext context)
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(distinctCount);
        ArgumentOutOfRangeException.ThrowIfLessThan(length, distinctCount);

        // a single distinct section cannot be repeated, so it can only fill a single slot
        if (distinctCount == 1)
            ArgumentOutOfRangeException.ThrowIfGreaterThan(length, 1);

        return _paint(distinctCount, length, context);
    }

    private static ImmutableArray<int> Build(int length, Func<int, int> indexSelector)
    {
        var builder = ImmutableArray.CreateBuilder<int>(length);
        for (var i = 0; i < length; i++)
            builder.Add(indexSelector(i));
        return builder.ToImmutable();
    }
}

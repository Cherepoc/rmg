using System.Collections.Immutable;
using Rmg.Core.Probabilities;

namespace Rmg.Core.Composition;

/// <summary>
///     Which bar pattern each bar of a 4-bar pattern plays, as letters: bars of the same letter play the same bar
///     pattern, so AABA plays one pattern three times and another once. A repeat can be varied (A′): it plays the same
///     bar pattern with its cycles drawn afresh more often, so it starts as the first did and then changes.
/// </summary>
/// <param name="Letters">Every bar's letter, as the index of its bar pattern, from 0.</param>
/// <param name="IsVaried">Whether every bar is a varied repeat.</param>
public sealed record PhraseScheme(ImmutableArray<int> Letters, ImmutableArray<bool> IsVaried)
{
    public int PatternCount => Letters.Max() + 1;

    /// <summary>The scheme as letters, such as AABA, a varied repeat marked with a prime.</summary>
    public override string ToString()
    {
        return string.Concat(Letters.Select((letter, bar) => (char)('A' + letter) + (IsVaried[bar] ? "′" : "")));
    }
}

public static class PhraseSchemes
{
    /// <summary>The chance of a repeat being varied.</summary>
    public const double VariedRepeatChance = 0.25;

    /// <summary>How much more often a varied repeat draws its cycles afresh.</summary>
    public const double VariedRepeatVariation = 0.5;

    /// <summary>
    ///     The schemes and how likely each is: repeats, which give a phrase its identity, most of all, and a phrase of
    ///     four different bars rarely.
    /// </summary>
    public static ImmutableArray<(string Scheme, double Weight)> All { get; } =
    [
        ("AAAA", 0.15),
        ("AABA", 0.2),
        ("AAAB", 0.2),
        ("ABAB", 0.15),
        ("AABB", 0.1),
        ("ABAC", 0.1),
        ("ABCD", 0.05)
    ];

    /// <summary>
    ///     How unconventional a scheme is, by how many different bars it brings: -0.5 for one bar four times, 0 for two,
    ///     and 1 for four different bars.
    /// </summary>
    public static double GetUnconventionality(string scheme)
    {
        return (scheme.Distinct().Count() - 2) / 2.0;
    }

    /// <summary>
    ///     A scheme, those that bring more new bars the more likely the more unconventional the rhythm, and some of its
    ///     repeats varied.
    /// </summary>
    public static PhraseScheme Pick(IGenerationContext context, RhythmicUnconventionality unconventionality)
    {
        ImmutableArray<Weighted<string>> weights =
        [
            ..All.Select(x => new Weighted<string>(unconventionality.Tilt.Weigh(x.Weight, GetUnconventionality(x.Scheme)), x.Scheme))
        ];
        var scheme = weights[Generators.WeightedIndex(weights)(context)].Value;

        var letters = scheme.Select(x => x - 'A').ToImmutableArray();
        var isVaried = letters
            .Select((letter, bar) => letters[..bar].Contains(letter) && context.TestProbability(VariedRepeatChance))
            .ToImmutableArray();
        return new PhraseScheme(letters, isVaried);
    }
}

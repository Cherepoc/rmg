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

    /// <summary>The scheme over a section's pattern of several phrases, every phrase playing its bars as the first does, over its own chords.</summary>
    public PhraseScheme Across(int phrases) => phrases == 1
        ? this
        : new PhraseScheme([..Enumerable.Repeat(Letters, phrases).SelectMany(x => x)], [..Enumerable.Repeat(IsVaried, phrases).SelectMany(x => x)]);

    /// <summary>The scheme with every repeat played as it was first, none varied.</summary>
    public PhraseScheme Unvaried() => this with { IsVaried = [..IsVaried.Select(_ => false)] };

    /// <summary>The scheme as letters, such as AABA, a varied repeat marked with a prime.</summary>
    public override string ToString()
    {
        return string.Concat(Letters.Select((letter, bar) => (char)('A' + letter) + (IsVaried[bar] ? "′" : "")));
    }
}

internal static class PhraseSchemes
{
    /// <summary>The chance of a repeat being varied, by the form facet: never at the plain end, a repeat played as it was, and every one at the wild.</summary>
    public static ByConvention VariedRepeatChance { get; } = new(0, 0.25, 1);

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

    /// <summary>Every scheme's weights at the ends, by how unconventional it is.</summary>
    public static ImmutableArray<(string Scheme, ByConvention Weight)> Weights { get; } =
        [..All.Select(x => (x.Scheme, RhythmicUnconventionality.WeightEnds(x.Weight, GetUnconventionality(x.Scheme))))];

    /// <summary>
    ///     How unconventional a scheme is, by how many different bars it brings: -0.5 for one bar four times, 0 for two,
    ///     and 1 for four different bars.
    /// </summary>
    public static double GetUnconventionality(string scheme)
    {
        return (scheme.Distinct().Count() - 2) / 2.0;
    }

    /// <summary>
    ///     A scheme, those that bring more new bars the more likely the less conventional the section's form: the plainest
    ///     a bar played throughout or two bars, the wildest never one; and some of its repeats varied.
    /// </summary>
    /// <param name="unconventionality">The form facet of the section's unconventionality.</param>
    /// <param name="genre">The genre whose pool of schemes it is drawn among, where it names one.</param>
    public static PhraseScheme Pick(IGenerationContext context, double unconventionality, Genre genre)
    {
        var scheme = context.Pick(ByConvention.Weigh(Weights, unconventionality, genre.Pool<string>(GenreChoices.PhraseScheme, x => x.ToLowerInvariant())));

        var letters = scheme.Select(x => x - 'A').ToImmutableArray();
        var isVaried = letters
            .Select((letter, bar) => letters[..bar].Contains(letter) && context.TestProbability(VariedRepeatChance.At(unconventionality)))
            .ToImmutableArray();
        return new PhraseScheme(letters, isVaried);
    }
}

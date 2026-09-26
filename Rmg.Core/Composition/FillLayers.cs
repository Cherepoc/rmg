using System.Collections.Immutable;
using Rmg.Core.Probabilities;

namespace Rmg.Core.Composition;

/// <summary>What the drums land on at the downbeat after a line.</summary>
public enum FillLanding
{
    None,
    Kick,
    CrashAndKick
}

/// <summary>
///     How the drums mark the lines between sections and phrases: the fill before a line, and what they land on after
///     it. A section change is marked most, a phrase line inside a section now and then.
/// </summary>
public static class FillLayers
{
    /// <summary>What the drums land on at a section change, and how likely each is.</summary>
    public static ImmutableArray<Weighted<FillLanding>> SectionLandings { get; } =
    [
        new(0.65, FillLanding.CrashAndKick),
        new(0.2, FillLanding.Kick),
        new(0.15, FillLanding.None)
    ];

    /// <summary>The crashes of the cymbal, by their sound's number, from 1: the first crash, then the second.</summary>
    public static ImmutableArray<Weighted<int>> Crashes { get; } =
    [
        new(0.7, 1),
        new(0.3, 4)
    ];

    /// <summary>How loud a landing's hit is over the drum's state there, as a note's accent.</summary>
    public const double LandingVelocity = 0.8;
}

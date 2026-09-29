using System.Collections.Immutable;
using Rmg.Core.Probabilities;

namespace Rmg.Core.Composition;

/// <summary>
///     How much the bass leads into the next chord, drawn by layer: the bass instrument sets where the song starts,
///     and a section moves it. The amount sets, bar by bar, how often its last note before a change leads into it (see
///     <c>StateKinds.ChordApproach</c>); what it lands on in the new chord leans by the section's conventionality alone
///     (<see cref="Arrivals" />, <c>StateKinds.ChordArrival</c>).
/// </summary>
public static class BassLeadingLayers
{
    /// <summary>Upright, acoustic and fretless basses, which walk into their chords.</summary>
    public const double Walking = 0.8;

    /// <summary>Electric basses and the low winds, in between.</summary>
    public const double Moderate = 0.55;

    /// <summary>Synth basses, which mostly sit on the roots.</summary>
    public const double Plain = 0.15;

    /// <summary>How far the song moves away from its bass's amount, either way.</summary>
    public const double Song = 0.15;

    /// <summary>How far a section moves away from the song's amount, either way.</summary>
    public const double Section = 0.3;

    /// <summary>The chance of a bar leading into the next at the most leading.</summary>
    public const double MaxApproachChance = 0.8;

    /// <summary>
    ///     What the bass lands on in a new chord, and how likely each is, whatever the bass: mostly the root, now and then
    ///     an inversion or the note its figure plays; all but the root lean unconventional.
    /// </summary>
    public static ImmutableArray<Weighted<ChordArrival>> Arrivals { get; } =
    [
        new(0.8, ChordArrival.Root),
        new(0.05, ChordArrival.Third),
        new(0.05, ChordArrival.Fifth),
        new(0.1, ChordArrival.Free)
    ];

    /// <summary>How a bar leads into the next, and how likely each way is when it does.</summary>
    public static ImmutableArray<Weighted<ChordApproach>> Approaches { get; } =
    [
        new(0.4, ChordApproach.ScaleStep),
        new(0.25, ChordApproach.HalfStepBelow),
        new(0.15, ChordApproach.Fifth),
        new(0.1, ChordApproach.HalfStepAbove),
        new(0.1, ChordApproach.Anticipation)
    ];

    /// <summary>A layer's shift of the amount, up to the given size either way.</summary>
    public static Func<IGenerationContext, double> CreateGenerator(double size)
    {
        return Generators.SplineValue().Then(x => x * size);
    }
}

/// <summary>How the bass's last note before a chord change leads into the next chord's root.</summary>
public enum ChordApproach
{
    None = 0,

    /// <summary>The scale note next to the root, from the side the line comes from.</summary>
    ScaleStep = 1,

    /// <summary>A semitone below the root, in the scale or not.</summary>
    HalfStepBelow = 2,

    /// <summary>A semitone above the root.</summary>
    HalfStepAbove = 3,

    /// <summary>The next chord's fifth, which falls to its root as a dominant does.</summary>
    Fifth = 4,

    /// <summary>The root itself, a little early.</summary>
    Anticipation = 5
}

/// <summary>What the bass plays on the first note of a new chord.</summary>
public enum ChordArrival
{
    /// <summary>The note its line would play anyway.</summary>
    Free = 0,

    Root = 1,

    /// <summary>The chord's third, for an inversion.</summary>
    Third = 2,

    /// <summary>The chord's fifth, for an inversion.</summary>
    Fifth = 3
}

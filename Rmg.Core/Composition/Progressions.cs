using System.Collections.Immutable;
using Rmg.Core.Probabilities;

namespace Rmg.Core.Composition;

/// <summary>
///     The chord progression of a section: the root of every bar of its 4-bar pattern, in scale steps from the
///     section's home. Bar 1 is the home chord, bar 2 moves away, bar 3 prepares the cadence and bar 4 is the cadence,
///     which resolves to bar 1 when the pattern plays again. Each bar's root is drawn by how strongly the previous root
///     leads to it, falling a fifth the most, and by how well it suits its bar. Roots stay within 3 steps of home, so
///     the progression keeps to one register.
/// </summary>
public static class Progressions
{
    public const int BarCount = 4;

    // the chord roots and the rules below are in steps of a 7-note scale, which every scale of Scales is
    private const int StepCount = 7;

    /// <summary>How much a root that does not suit its bar weighs against one that does.</summary>
    private const double OutOfRoleWeight = 0.05;

    /// <summary>
    ///     How strongly a root leads to the next, by how many steps up the scale the next is: falling a fifth (3 steps
    ///     up, the same as a fifth down) the most, then falling a third and moving a step, and the same root rarely.
    /// </summary>
    private static readonly ImmutableArray<double> MotionWeights =
    [
        0.1, // the same root
        0.5, // a step up
        0.35, // a third up
        1, // a fourth up, a fifth down
        0.3, // a fifth up, a fourth down
        0.6, // a third down
        0.4 // a step down
    ];

    /// <summary>The roots that prepare a cadence: ii and IV.</summary>
    private static readonly ImmutableHashSet<int> PreCadenceSteps = [1, 3];

    /// <summary>
    ///     The step of a section's home above the song's tonic, from -3 to 3: the tonic itself most often, the relative
    ///     key (the third degree of a minor scale, the sixth of a major one) sometimes, and IV or V now and then.
    /// </summary>
    public static int GenerateHome(IGenerationContext context, Scale scale)
    {
        var relativeStep = GetQuality(scale.Offsets, 0) == TriadQuality.Minor ? 2 : 5;
        ImmutableArray<Weighted<int>> homes =
        [
            new(0.6, 0),
            new(0.2, Normalize(relativeStep)),
            new(0.1, Normalize(3)),
            new(0.1, Normalize(4))
        ];
        return homes[Generators.WeightedIndex(homes)(context)].Value;
    }

    /// <summary>
    ///     The roots of the bars, in steps above the section's home, from -3 to 3.
    /// </summary>
    /// <param name="home">The step of the section's home above the song's tonic, which sets the chords around it.</param>
    /// <param name="strictness">
    ///     How closely the progression keeps to its rules, from 1, which follows them, to 0, which picks every root
    ///     as likely as any other.
    /// </param>
    public static ImmutableArray<int> Generate(IGenerationContext context, Scale scale, int home, double strictness)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(strictness);
        ArgumentOutOfRangeException.ThrowIfGreaterThan(strictness, 1);

        var cadenceWeights = GetCadenceWeights(scale.Offsets, home);
        var roots = new int[BarCount];
        for (var bar = 1; bar < BarCount; bar++)
        {
            var previous = roots[bar - 1];
            var weights = new Weighted<int>[StepCount];
            for (var step = 0; step < StepCount; step++)
            {
                var roleWeight = bar switch
                {
                    2 => PreCadenceSteps.Contains(step) ? 1 : OutOfRoleWeight,
                    3 => cadenceWeights[step] > 0 ? cadenceWeights[step] : OutOfRoleWeight,
                    _ => 1
                };
                var motionWeight = MotionWeights[Mod(step - previous)];
                weights[step] = new Weighted<int>(Math.Pow(motionWeight * roleWeight, strictness), step);
            }

            roots[bar] = Normalize(weights[Generators.WeightedIndex([..weights])(context)].Value);
        }

        return [..roots];
    }

    /// <summary>
    ///     How well each root, in steps above the home, ends a phrase that returns home. They follow from the chords
    ///     the scale builds around the home, so the cadence suits the mode: a major chord on the fifth (V), or a
    ///     minor one less; a major chord a whole step below home (♭VII, as in mixolydian or natural minor); a major
    ///     chord a half step above it (♭II, phrygian) or a whole step above it (II, lydian); the leading-tone chord a
    ///     half step below it a little; IV, more when it is major over a minor home (dorian); and VI now and then, which
    ///     does not resolve.
    /// </summary>
    public static ImmutableArray<double> GetCadenceWeights(ImmutableArray<int> scaleOffsets, int home)
    {
        if (scaleOffsets.Length != StepCount)
            throw new ArgumentException($"The scale must have {StepCount} notes.", nameof(scaleOffsets));

        var weights = new double[StepCount];
        var homeQuality = GetQuality(scaleOffsets, home);
        for (var step = 1; step < StepCount; step++)
        {
            var quality = GetQuality(scaleOffsets, home + step);
            var interval = GetInterval(scaleOffsets, home, home + step);
            weights[step] = step switch
            {
                4 => quality switch { TriadQuality.Major => 1, TriadQuality.Minor => 0.5, _ => 0.2 },
                6 when interval == 10 => quality == TriadQuality.Major ? 1 : 0.3,
                6 when interval == 11 => 0.2,
                1 when quality == TriadQuality.Major => interval == 1 ? 1 : 0.7,
                3 => quality == TriadQuality.Major && homeQuality == TriadQuality.Minor ? 0.8 : 0.3,
                5 => 0.1,
                _ => 0
            };
        }

        return [..weights];
    }

    /// <summary>
    ///     The step of the scale, above the song's tonic, to raise a semitone on the cadence bar so that a minor chord
    ///     on the fifth turns major and leads home as strongly as in harmonic minor; none when the cadence is another
    ///     chord, when the fifth's chord is major already, or when raising its third would not make it major, as in
    ///     phrygian. It is the step a whole step below the home, as in natural minor, dorian and mixolydian.
    /// </summary>
    /// <param name="cadenceRoot">The cadence bar's root, in steps above the home.</param>
    public static int? GetCadenceRaisedStep(ImmutableArray<int> scaleOffsets, int home, int cadenceRoot)
    {
        if (Mod(cadenceRoot) != 4 || GetQuality(scaleOffsets, home + 4) != TriadQuality.Minor)
            return null;

        var seventh = Mod(home + 6);
        if (GetInterval(scaleOffsets, seventh, home) != 2)
            return null;

        var raised = scaleOffsets.ToArray();
        raised[seventh]++;
        return GetQuality([..raised], home + 4) == TriadQuality.Major ? seventh : null;
    }

    /// <summary>The value a chord root offset takes to move the root by the steps, as a fraction of the scale.</summary>
    public static double ToRootOffset(int steps)
    {
        return steps / (double)StepCount;
    }

    private enum TriadQuality
    {
        Major,
        Minor,
        Other
    }

    /// <summary>The quality of the triad the scale builds on a step: its third and fifth above the root.</summary>
    private static TriadQuality GetQuality(ImmutableArray<int> scaleOffsets, int step)
    {
        var third = GetInterval(scaleOffsets, step, step + 2);
        var fifth = GetInterval(scaleOffsets, step, step + 4);
        return (third, fifth) switch
        {
            (4, 7) => TriadQuality.Major,
            (3, 7) => TriadQuality.Minor,
            _ => TriadQuality.Other
        };
    }

    /// <summary>The semitones from one step of the scale up to another, within an octave.</summary>
    private static int GetInterval(ImmutableArray<int> scaleOffsets, int from, int to)
    {
        return (scaleOffsets[Mod(to)] - scaleOffsets[Mod(from)]).Mod(12);
    }

    private static int Mod(int step)
    {
        return step.Mod(StepCount);
    }

    /// <summary>The step, moved by octaves to within 3 steps of 0.</summary>
    private static int Normalize(int step)
    {
        return Mod(step + 3) - 3;
    }
}

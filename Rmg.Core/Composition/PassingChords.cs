using System.Collections.Immutable;
using Rmg.Core.Probabilities;

namespace Rmg.Core.Composition;

/// <summary>How a passing chord leads into the next.</summary>
public enum PassingKind
{
    /// <summary>The next chord's dominant, a fifth above it, in Mixolydian on its own root where the scale lets it.</summary>
    Dominant,

    /// <summary>The next chord itself a semitone above, its shape and its scale moved with it, sliding down into it.</summary>
    Chromatic
}

/// <summary>
///     Chords the progression does not have, passing into the next (<see cref="PassingKind" />), over the second half of
///     the chord before, a weak beat about its middle. One chord in the bar state, a change of its own, which every part
///     takes.
/// </summary>
internal static class PassingChords
{
    /// <summary>How likely each kind is, by the progression facet: the dominant the plain one, the chromatic a wild song's the more.</summary>
    public static ImmutableArray<(PassingKind Kind, ByConvention Weight)> Kinds { get; } =
    [
        (PassingKind.Dominant, new ByConvention(1, 1, 0.5)),
        (PassingKind.Chromatic, new ByConvention(0, 0.25, 1))
    ];

    /// <summary>
    ///     The chance a chord a bar long or longer passes into the next by its dominant, by the progression facet: never at
    ///     the plain end, which is never reharmonised, and as often at the wild end as in the middle.
    /// </summary>
    public static ByConvention Chance { get; } = new(0, 0.1, 0.1);

    /// <summary>How a section's energy leans the chance, through its odds: busier harmony in a busier section.</summary>
    public const double EnergyLean = 1;

    /// <summary>
    ///     The section's harmony with its passing chords: every chord but the last, a bar long or longer and not the next's
    ///     dominant already, passing into the next now and then, its kind by the progression facet, two draws a chord
    ///     whatever the chance; and every chord's shift in semitones, the chromatic ones' 1.
    /// </summary>
    /// <param name="progression">The chords' roots, in steps above the home.</param>
    /// <param name="chordScales">Every chord's own scale.</param>
    public static (HarmonicRhythm Rhythm, ImmutableArray<int> Progression, ImmutableArray<ChordScale> ChordScales, ImmutableArray<int> Shifts) Draw(
        IGenerationContext context, double chance, double unconventionality, HarmonicRhythm rhythm, ImmutableArray<int> progression, ImmutableArray<ChordScale> chordScales,
        Scale scale, int home)
    {
        var changes = rhythm.Changes;
        var kinds = ByConvention.Weigh(Kinds, unconventionality);
        var passing = ImmutableArray.CreateBuilder<double>();
        var (roots, scales, shifts) = (new List<int>(), new List<ChordScale>(), new List<int>());
        for (var chord = 0; chord < progression.Length; chord++)
        {
            roots.Add(progression[chord]);
            scales.Add(chordScales[chord]);
            shifts.Add(0);
            var passes = context.TestProbability(chance);
            var kind = context.Pick(kinds);
            var length = rhythm.LengthOf(chord);
            if (!passes || chord == progression.Length - 1 || length < rhythm.Meter.BarDuration - 1e-9)
                continue;

            var next = progression[chord + 1];
            var dominant = Normalize(next + 4);
            if (kind == PassingKind.Dominant && Mod(dominant - progression[chord]) == 0)
                continue;

            passing.Add(rhythm.Meter.NearestGroupStart(changes[chord] + length / 2));
            roots.Add(kind == PassingKind.Dominant ? dominant : next);
            scales.Add(kind == PassingKind.Dominant
                ? ChordScales.Toward(scale.Offsets, home + dominant, Scales.Mixolydian.Offsets, ChordScaleKind.Applied) ?? ChordScale.Section
                : chordScales[chord + 1]);
            shifts.Add(kind == PassingKind.Chromatic ? 1 : 0);
        }

        return passing.Count == 0
            ? (rhythm, progression, chordScales, [..shifts])
            : (rhythm with { Passing = passing.ToImmutable() }, [..roots], [..scales], [..shifts]);
    }

    private static int Mod(int step) => step.Mod(Scales.StepCount);

    private static int Normalize(int step) => Mod(step + 3) - 3;
}

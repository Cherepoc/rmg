using System.Collections.Immutable;
using Rmg.Core.Probabilities;

namespace Rmg.Core.Composition;

/// <summary>
///     Chords the progression does not have, passing into the next: the next chord's dominant, a fifth above it in
///     Mixolydian on its own root where the scale lets it, over the second half of the chord before, a weak beat about
///     its middle. One chord in the bar state, a change of its own, which every part takes.
/// </summary>
internal static class PassingChords
{
    /// <summary>
    ///     The chance a chord a bar long or longer passes into the next by its dominant, by the progression facet: never at
    ///     the plain end, which is never reharmonised, and as often at the wild end as in the middle.
    /// </summary>
    public static ByConvention Chance { get; } = new(0, 0.1, 0.1);

    /// <summary>How a section's energy leans the chance, through its odds: busier harmony in a busier section.</summary>
    public const double EnergyLean = 1;

    /// <summary>
    ///     The section's harmony with its passing chords: every chord but the last, a bar long or longer and not the next's
    ///     dominant already, passing into the next now and then, a draw a chord whatever the chance.
    /// </summary>
    /// <param name="progression">The chords' roots, in steps above the home.</param>
    /// <param name="chordScales">Every chord's own scale.</param>
    public static (HarmonicRhythm Rhythm, ImmutableArray<int> Progression, ImmutableArray<ChordScale> ChordScales) Draw(IGenerationContext context, double chance,
        HarmonicRhythm rhythm, ImmutableArray<int> progression, ImmutableArray<ChordScale> chordScales, Scale scale, int home)
    {
        var changes = rhythm.Changes;
        var passing = ImmutableArray.CreateBuilder<double>();
        var roots = new List<int>();
        var scales = new List<ChordScale>();
        for (var chord = 0; chord < progression.Length; chord++)
        {
            roots.Add(progression[chord]);
            scales.Add(chordScales[chord]);
            var passes = context.TestProbability(chance);
            var root = Normalize(chord + 1 < progression.Length ? progression[chord + 1] + 4 : 0);
            var length = rhythm.LengthOf(chord);
            if (!passes || chord == progression.Length - 1 || length < rhythm.Meter.BarDuration - 1e-9 || Mod(root - progression[chord]) == 0)
                continue;

            passing.Add(rhythm.Meter.NearestGroupStart(changes[chord] + length / 2));
            roots.Add(root);
            scales.Add(ChordScales.Toward(scale.Offsets, home + root, Scales.Mixolydian.Offsets, ChordScaleKind.Applied) ?? ChordScale.Section);
        }

        return passing.Count == 0 ? (rhythm, progression, chordScales) : (rhythm with { Passing = passing.ToImmutable() }, [..roots], [..scales]);
    }

    private static int Mod(int step) => step.Mod(Scales.StepCount);

    private static int Normalize(int step) => Mod(step + 3) - 3;
}

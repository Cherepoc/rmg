using Rmg.Core.Events;
using Rmg.Core.Probabilities;

namespace Rmg.Core.Composition;

/// <summary>
///     How busy a song's melody is, from 0 to 1: at 0 a sparse, held line of a note or two a bar, as in a ballad, at 1 a
///     busy one of riffs in 8ths. It moves the melody's rhythm settings in every section: how full its pattern is, and
///     how likely it is to play twice as fast. A cycle has at most four beats to play, so a busy melody needs the
///     shorter cycles. A song's is spread widely around the middle, and a section moves it, so that a verse can be
///     sparser than a chorus.
/// </summary>
internal sealed record MelodyBusyness(double Value)
{
    /// <summary>How far a section moves the song's value, either way.</summary>
    public const double SectionShift = 0.2;

    /// <summary>How much fuller a melody is than the other tracks, in the middle.</summary>
    public const double Fullness = 0.15;

    /// <summary>How far the fullness moves either way, at 0 and at 1.</summary>
    public const double FullnessRange = 0.3;

    /// <summary>
    ///     The chance that a section's melody plays twice as fast, such as in 8ths rather than quarters: this at 0,
    ///     rising to 1 at 1.
    /// </summary>
    public const double MinSpeedUpChance = 0.3;

    // around 0 with a flat peak, from -2 to 2, so that songs spread over the whole range and more of them near the middle
    private static readonly Func<IGenerationContext, double> SpreadGenerator = Generators.SplineValue(0);

    public static MelodyBusyness Generate(IGenerationContext context)
    {
        return new MelodyBusyness(Math.Clamp(0.5 + SpreadGenerator(context) / 4, 0, 1));
    }

    public MelodyBusyness GenerateSection(IGenerationContext context)
    {
        return new MelodyBusyness(Math.Clamp(Value + Generators.SplineValue()(context) * SectionShift, 0, 1));
    }

    /// <summary>The steps of the melody's rhythm settings in a section.</summary>
    public StateMapBuilder AddTo(StateMapBuilder builder)
    {
        return builder
            .Add(CompositionStateKinds.Rhythm.Fullness, Fullness + (Value - 0.5) * 2 * FullnessRange)
            .Add(CompositionStateKinds.Rhythm.Period.Power, context => context.TestProbability(MinSpeedUpChance + Value * (1 - MinSpeedUpChance)) ? -1 : 0);
    }
}

using Rmg.Core.Probabilities;

namespace Rmg.Core.Composition;

/// <summary>
///     How much a layer of the song moves a track's rhythm. Every layer adds a step of -1, 0 or 1 to each rhythm
///     setting, and the settings are the sums. A layer moves a setting with the chance it has for it, so that the
///     setting a drum is given, such as the snare's backbeat, stays the likeliest outcome while every layer still
///     varies it now and then.
/// </summary>
/// <param name="Groove">
///     The chance to move the settings that make the groove: how fast a track plays and where its strong beat falls.
///     They belong to the song and its sections, and change little inside them. A track playing twice as fast or as
///     slow keeps its groove, so its speed changes <see cref="SpeedShare" /> as often, which leaves a drum at the
///     speed it is given in about half of the bars.
/// </param>
/// <param name="Tuplet">
///     The chance to move the tuplet period, such as to a triplet feel or a 3+3+2. It is kept low for the whole song
///     and higher for a section or a bar, so that tuplets come and go as a passage or a fill.
/// </param>
/// <param name="Density">
///     The chance to move the settings that make how busy the rhythm is: how many subdivisions it has and which of
///     them play. They change freely, most of all from bar to bar.
/// </param>
public sealed record RhythmLayer(double Groove, double Density, double Tuplet)
{
    public const double SpeedShare = 3.25;

    public Func<IGenerationContext, int> CreateGrooveGenerator()
    {
        return CreateStepGenerator(Groove);
    }

    public Func<IGenerationContext, int> CreateSpeedGenerator()
    {
        return CreateStepGenerator(Groove * SpeedShare);
    }

    public Func<IGenerationContext, int> CreateTupletGenerator()
    {
        return CreateStepGenerator(Tuplet);
    }

    public Func<IGenerationContext, int> CreateDensityGenerator()
    {
        return CreateStepGenerator(Density);
    }

    /// <summary>A step of -1 or 1 with the chance given, each as likely, and 0 otherwise.</summary>
    internal static Func<IGenerationContext, int> CreateStepGenerator(double chance)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(chance);
        ArgumentOutOfRangeException.ThrowIfGreaterThan(chance, 1);

        return context =>
        {
            if (!context.TestProbability(chance))
                return 0;

            return context.GenerateInt(0, 2) == 0 ? -1 : 1;
        };
    }
}

/// <summary>
///     The layers, from the song down to a bar. Their chances are small, so that together they leave most bars in the
///     groove the tracks are given: the snare keeps its backbeat in most bars, with a busier or shifted bar now and then.
///     Tuplets play in about a fifth of a drum's bars, mostly as a section or a bar rather than a whole song.
/// </summary>
public static class RhythmLayers
{
    public static RhythmLayer Song { get; } = new(0.075, 0.1, 0.025);

    public static RhythmLayer Section { get; } = new(0.05, 0.1, 0.07);

    /// <summary>A track's own rhythm for the whole song.</summary>
    public static RhythmLayer Track { get; } = new(0.025, 0.05, 0.008);

    /// <summary>How a track's rhythm differs in a section from the song.</summary>
    public static RhythmLayer SectionTrack { get; } = new(0.025, 0.05, 0.035);

    /// <summary>The rhythm the drums share for the whole song.</summary>
    public static RhythmLayer DrumGroup { get; } = new(0.025, 0.05, 0.008);

    /// <summary>How the drums' shared rhythm differs in a section.</summary>
    public static RhythmLayer SectionDrumGroup { get; } = new(0.025, 0.05, 0.035);

    /// <summary>A track's bar pattern, where a busier or sparser bar sounds like a variation, not a new groove.</summary>
    public static RhythmLayer BarPattern { get; } = new(0.025, 0.15, 0.05);
}

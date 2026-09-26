using Rmg.Core.Probabilities;

namespace Rmg.Core.Composition;

/// <summary>
///     How smoothly the chords of a track move (see <c>StateKinds.VoiceLeading</c>), drawn by layer like the velocity:
///     the chord instrument sets where the song starts, a section moves it, and now and then a bar starts afresh in
///     its own register.
/// </summary>
public static class VoiceLeadingLayers
{
    /// <summary>Strings, pads, organs and choirs, which hold their notes and move them little.</summary>
    public const double Sustained = 0.8;

    /// <summary>Pianos and the other keyboards and mallets, in between.</summary>
    public const double Piano = 0.6;

    /// <summary>Guitars, which mostly move one chord shape up and down the neck.</summary>
    public const double Guitar = 0.3;

    /// <summary>How far the song moves away from its instrument's smoothness, either way.</summary>
    public const double Song = 0.15;

    /// <summary>How far a section moves away from the song's smoothness, either way.</summary>
    public const double Section = 0.4;

    /// <summary>The chance of a 4-bar pattern starting afresh, which marks a new section by a new register.</summary>
    public const double ResetAtPatternStart = 0.25;

    /// <summary>The chance of any other bar starting afresh, a deliberate break.</summary>
    public const double ResetElsewhere = 0.03;

    /// <summary>A layer's shift of the smoothness, up to the given size either way.</summary>
    public static Func<IGenerationContext, double> CreateGenerator(double size)
    {
        return Generators.SplineValue().Then(x => x * size);
    }
}

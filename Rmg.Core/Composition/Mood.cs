using System.Collections.Immutable;
using Rmg.Core.Probabilities;

namespace Rmg.Core.Composition;

/// <summary>
///     A song's mood, beside its unconventionality: how dark it is, from -1, the brightest, to 1, the darkest, and how
///     intense, from -1, the calmest, to 1, the fiercest, the two axes of valence and arousal, their quadrants serene,
///     euphoric, sombre and menacing. A mood never adds an option nor takes one away: it leans the weights of those a
///     genre's pool allows at the song's conventionality, an option's odds times <see cref="Odds" /> to the power of its
///     lean times the mood, as energy leans; a middling mood leans nothing. Each is drawn within a genre's span
///     (<see cref="GenreChoices.Darkness" />, <see cref="GenreChoices.Intensity" />), the songs' own a moderate one, or
///     given, from a sequence of its own, so that giving one leaves the rest of the song as its seed made it.
/// </summary>
public readonly record struct Mood(double Darkness, double Intensity)
{
    /// <summary>How much likelier an option leaning all the way one way is at that end of a mood: four times.</summary>
    public const double Odds = 4;

    /// <summary>The songs' own span of either, as a range's amounts: -0.6 to 0.6, gathering at the middle.</summary>
    public static (double From, double To, double Lean) SongsSpan { get; } = (0.2, 0.8, 0);

    /// <summary>A mood as a range's amount gives it, from 0, -1, to 1, 1.</summary>
    public static double OfAmount(double amount) => 2 * amount - 1;

    /// <summary>A mood's amount in a range (<see cref="OfAmount" />).</summary>
    public static double AmountOf(double mood) => (mood + 1) / 2;

    /// <summary>How far a mood moves a range's lean, as its intensity moves a tempo toward the fast end of a genre's.</summary>
    public const double RangeShift = 0.25;

    /// <summary>
    ///     The options a mood leans, by their choice: each option's lean by darkness and by intensity, from -1 to 1, 0 for
    ///     none; a choice's options it does not name lean none; and whether a song of a genre that does not name the choice
    ///     leans it as the songs have it, where its options' own amounts are the songs' draw, and not where the songs draw
    ///     otherwise, as their kit by their drums' job. The scales by their darkness, Dorian's none, Lydian bright and
    ///     Locrian dark (<see cref="Scale.Darkness" />); the fiercer, the likelier the power kit, double time, a riff that
    ///     chugs or plays power chords, and the calmer the brushes, the jazz kit and half time; a tempo's range leaning to
    ///     its fast end the fiercer.
    /// </summary>
    // built when first asked, as the choices it names take the songs' span of a mood from here
    public static ImmutableArray<(GenreChoice Choice, Func<string, (double Darkness, double Intensity)> Lean, bool AsSongs)> Leans => field.IsDefault ? field =
    [
        (GenreChoices.Scale, name => (Scales.All.Single(x => GenreChoices.Name(x.Name) == name).Darkness, 0), true),
        (GenreChoices.Kit, name => (0, name switch { "power" => 1, "room" => -0.3, "jazz" => -0.7, "brush" => -1, _ => 0 }), false),
        (GenreChoices.FeelChances, name => (0, name switch { "doubleTime" => 1, "halfTime" => -0.5, _ => 0 }), true),
        (GenreChoices.Riff, name => (0, name switch { "chugs" => 1, "powerChords" => 0.5, _ => 0 }), true),
        (GenreChoices.Tempo, name => (0, name == "lean" ? 1 : 0), false)
    ] : field;

    /// <summary>How far an option's lean moves its odds under this mood, in log-odds.</summary>
    public double LogOdds((double Darkness, double Intensity) lean) => Math.Log(Odds) * (lean.Darkness * Darkness + lean.Intensity * Intensity);
}

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

    /// <summary>The lean of options by how dark each is, from -1, bright, to 1, dark.</summary>
    public Tilt Darker => Tilt.Of(Odds, Darkness);

    /// <summary>The lean of options by how fierce each is, from -1, calm, to 1, fierce.</summary>
    public Tilt Fiercer => Tilt.Of(Odds, Intensity);
}

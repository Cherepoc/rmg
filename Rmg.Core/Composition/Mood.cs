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
    ///     otherwise, as their kit by their drums' job. The scales by their darkness, a minor third's dark and a major's
    ///     bright, Lydian the brightest and Locrian the darkest (<see cref="Scale.Darkness" />); the fiercer, the likelier
    ///     the power kit, double time, a riff that chugs or plays power chords, a driven instrument, a loud section's figure,
    ///     hat rolls, ghost notes and drops, and the calmer the brushes, the jazz kit and half time; a tempo's range leaning
    ///     to its fast end the fiercer, and its fast third; a chord shape and a chord's own mode by its darkness; fills the
    ///     more the fiercer, a value as a range's lean.
    /// </summary>
    // built when first asked, as the choices it names take the songs' span of a mood from here
    public static ImmutableArray<(GenreChoice Choice, Func<string, (double Darkness, double Intensity)> Lean, bool AsSongs)> Leans => field.IsDefault ? field =
    [
        (GenreChoices.Scale, name => (Scales.All.Single(x => GenreChoices.Name(x.Name) == name).Darkness, 0), true),
        (GenreChoices.Kit, name => (0, name switch { "power" => 1, "room" => -0.3, "jazz" => -0.7, "brush" => -1, _ => 0 }), false),
        (GenreChoices.FeelChances, name => (0, name switch { "doubleTime" => 1, "halfTime" => -0.5, _ => 0 }), true),
        (GenreChoices.Riff, name => (0, name switch { "chugs" => 1, "powerChords" => 0.5, _ => 0 }), true),
        (GenreChoices.Tempo, name => (0, name == "lean" ? 1 : 0), false),
        // and a genre's thirds of its tempo range, the fast the fiercer and the slow the calmer
        (GenreChoices.TempoBand, name => (0, name switch { "fast" => 1, "slow" => -1, _ => 0 }), false),
        // fills the more the fiercer, a value moved as a range's lean is
        (GenreChoices.Fills, name => (0, 1), true),
        // the chord shapes by their darkness, a Phrygian or a diminished chord the darker and a Lydian or a six-nine the
        // brighter, where a genre names its pool, as the songs draw theirs by levels first
        (GenreChoices.ChordShape, name => (ChordShapes.All.Single(x => GenreChoices.Name(x.Name) == name).Darkness, 0), false),
        // a chord's own mode by its darkness, as the scales are, where a genre names its pool
        (GenreChoices.OwnMode, name => (ChordScales.OwnModes.Single(x => GenreChoices.Name(x.Mode.Name) == name).Mode.Darkness, 0), false),
        // the drums' energy, the more the fiercer, where a genre names it: rolls on the hi-hat, ghost notes, builds to a drop
        (GenreChoices.HatRolls, _ => (0, 1), false),
        (GenreChoices.Ghosts, _ => (0, 1), false),
        (GenreChoices.Drop, name => (0, name == "lifts" ? 1 : 0), false),
        // every player's figures by how a loud section leans them, a double kick or four on the floor the fiercer
        ..Enum.GetValues<FigurePlayer>().Select(player => (GenreChoices.Figures[player],
            (Func<string, (double, double)>)(name => (0, Figures.Of(player, name)?.EnergyLean ?? 0)), false)),
        // every part's instruments, a driven one the fiercer
        ..GenreChoices.InstrumentPools.Select(x => (GenreChoices.Instruments[x.Role],
            (Func<string, (double, double)>)(name => (0, InstrumentTraits.Has(x.Pool.Everything.First(i => GenreChoices.Name(i.Name) == name).Program, InstrumentTrait.Driven) ? 1 : 0)),
            true))
    ] : field;

    /// <summary>How far this mood leans an option of a lean, its darkness's and its intensity's together, from -1 to 1 at its ends.</summary>
    public double Lean((double Darkness, double Intensity) lean) => lean.Darkness * Darkness + lean.Intensity * Intensity;

    /// <summary>How this mood moves the odds of an option of a lean, by its odds to the power of the lean (<see cref="Lean" />).</summary>
    public Tilt Tilt((double Darkness, double Intensity) lean) => Probabilities.Tilt.Of(Odds, Lean(lean));
}

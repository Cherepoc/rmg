using System.Collections.Immutable;

namespace Rmg.Core.Composition;

/// <summary>The General MIDI sounds of the drums that the fills name, by their note numbers.</summary>
internal static class DrumSounds
{
    public const int ClosedHiHat = 42;
    public const int PedalHiHat = 44;
    public const int OpenHiHat = 46;

    public const int CrashCymbal1 = 49;

    public const int CrossStick = 37;
    public const int RideBell = 53;
    public const int China = 52;

    /// <summary>The sounds a genre leans (<see cref="GenreChoices.DrumSounds" />), by its names for them.</summary>
    public static ImmutableArray<(string Name, string Label, int Code)> Leaned { get; } =
    [
        ("crossStick", "The snare's cross-stick", CrossStick), ("closedHiHat", "The closed hi-hat", ClosedHiHat), ("pedalHiHat", "The hi-hat's foot", PedalHiHat),
        ("openHiHat", "The open hi-hat", OpenHiHat),
        ("rideBell", "The ride's bell", RideBell), ("china", "The china", China)
    ];

    /// <summary>How far a genre leans a sound at its most and its least, as odds: eight times as likely, or an eighth.</summary>
    public const double GenreOdds = 8;

    /// <summary>
    ///     How much likelier a genre has each sound, by its note number, as the choice given has it: its steady stroke
    ///     (<see cref="GenreChoices.DrumStrokes" />), or an accent and in a fill (<see cref="GenreChoices.DrumSounds" />); 1
    ///     for a sound it does not lean.
    /// </summary>
    public static Func<int, double> OddsOf(Genre genre, GenreChoice choice)
    {
        var odds = Leaned.ToDictionary(x => x.Code, x => Math.Pow(GenreOdds, genre.Shift(choice, x.Name)));
        return code => odds.GetValueOrDefault(code, 1);
    }
}

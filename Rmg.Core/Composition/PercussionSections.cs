using Rmg.Core.Probabilities;

namespace Rmg.Core.Composition;

/// <summary>
///     Sections that play their percussion without the drum kit, so that a song switches to percussion and back at
///     section lines. A section plays percussion only by a chance of the song's lean and its own: the song's lean is
///     drawn around none, and a song that leans far enough plays percussion in most of its sections; a section's leans
///     by how unconventional its rhythm is and, the other way, by its energy, as the percussion alone is the quieter
///     groove. Only a song with two or more percussion drums has such sections, so that the percussion can groove.
/// </summary>
internal static class PercussionSections
{
    /// <summary>The chance a section plays percussion only, with no lean.</summary>
    public const double Chance = 0.06;

    /// <summary>How far the song's lean spreads either way, as the log of the odds it multiplies the chance's by.</summary>
    public const double SongSpread = 2;

    /// <summary>How few percussion drums a song has for its sections to play them alone.</summary>
    public const int MinDrums = 2;

    /// <summary>
    ///     The share of its sections the wildest song plays percussion only: the song plays them every time, but not
    ///     everywhere, so that its kit is heard too.
    /// </summary>
    public const double WildShare = 0.5;

    /// <summary>How many of the song's percussion drums a section of percussion only plays at most.</summary>
    public const int MaxActiveDrums = 4;

    /// <summary>
    ///     How far the song leans to sections of percussion only, around none; a song of percussion alone is a setup of
    ///     its own (<see cref="DrumSetups" />).
    /// </summary>
    public static Tilt GenerateSong(IGenerationContext context)
    {
        return new Tilt(Generators.SplineValue()(context) * SongSpread);
    }

    /// <summary>Whether a section plays percussion only; drawn only where the song has enough percussion.</summary>
    /// <param name="song">How far the song leans to such sections.</param>
    /// <param name="unconventionality">The section's groove facet: never percussion only at the plain end, and <see cref="WildShare" /> of the time at the wild.</param>
    /// <param name="energy">The section's pull of its energy, which leans it to the drum kit.</param>
    /// <param name="songPercussion">How many percussion drums the song has.</param>
    public static bool Draw(IGenerationContext context, Tilt song, double unconventionality, Tilt energy, int songPercussion)
    {
        return songPercussion >= MinDrums && context.TestProbability(energy.Chance(new ByConvention(0, song.Chance(Chance, 1), WildShare).At(unconventionality), -1));
    }
}

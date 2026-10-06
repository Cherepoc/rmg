using Rmg.Core.Songs;
using Rmg.Core.Events;

namespace Rmg.Core.Composition;

/// <summary>
///     A genre's rhythm of a part or a drum role (<see cref="GenreChoices.Rhythms" />): a layer at the song's depth of its
///     rhythm settings, added to the part's state as a drum role's part is (<see cref="DrumRoles.Parts" />), so that every
///     layer below still varies around it and a plain song plays it as it is. A shift means what the part's own state
///     makes of it: half a cycle off the beat is an 8th's offbeat for a part in quarters, and beat 3 for one in bars.
/// </summary>
internal static class GenreRhythm
{
    /// <summary>
    ///     How many steps the most of a value moves a rhythm setting: two, so that a part of a bar's cycle can play a beat's,
    ///     one of a beat's cycle a 16th off it, and one down to 16ths play its cycle's beats alone. A setting moves only away
    ///     from the end of its range where the parts rest, as a step past an end folds back: how fine a part goes down from
    ///     its finest, where the song sets it, and its offset off the beat and its weight on weaker beats up from none.
    /// </summary>
    public const int Steps = 2;

    /// <summary>How far the most of a value moves the fullness, either way.</summary>
    public const double FullnessRange = 0.4;

    /// <summary>How far the most of a value moves the variation, either way: the more repetition, the less.</summary>
    public const double VariationRange = 0.5;

    /// <summary>How far the most of a value moves the melody's busyness, either way.</summary>
    public const double BusynessRange = 0.5;

    /// <summary>The settings a genre may move of a part's rhythm, by their names, and how each shows.</summary>
    public static IReadOnlyList<(string Name, string Label)> Settings { get; } =
    [
        ("speed", "Speed"),
        ("offbeat", "Off the beat"),
        ("fineness", "Finest notes"),
        ("syncopation", "Weight on weaker beats"),
        ("fullness", "Fullness"),
        ("repetition", "Repetition")
    ];

    /// <summary>
    ///     The genre's layer of a part's rhythm, times the sign given, so that a section can take one role's and add
    ///     another's; none where the genre does not name it. Off the beat moves only off it: its least is on the beat, as
    ///     the part is, as is the most of how fine it goes and the least of its weight on weaker beats (<see cref="Steps" />).
    /// </summary>
    public static StateMap Of(Genre genre, GenreChoice choice, int sign = 1)
    {
        if (!genre.Names(choice))
            return StateMap.Default;

        double Shift(string setting) => genre.Shift(choice, setting);
        int Step(double shift) => sign * (int)Math.Round(Steps * shift, MidpointRounding.AwayFromZero);
        IState[] states =
        [
            CompositionStateKinds.Rhythm.Period.Power.CreateState(Step(-Shift("speed"))),
            CompositionStateKinds.Rhythm.Phase.Rank.CreateState(Step(Math.Max(0, Shift("offbeat")))),
            CompositionStateKinds.Rhythm.MaxRank.CreateState(Step(Math.Min(0, Shift("fineness")))),
            CompositionStateKinds.Rhythm.RankOffset.CreateState(Step(Math.Max(0, Shift("syncopation")))),
            CompositionStateKinds.Rhythm.Fullness.CreateState(sign * FullnessRange * Shift("fullness")),
            CompositionStateKinds.Rhythm.Variation.CreateState(-sign * VariationRange * Shift("repetition"))
        ];
        return StateMap.FromStates([..states.Where(x => !x.IsDefault)]);
    }

    /// <summary>The part of a pitched role whose rhythm a genre may move; none for one that has none of its own.</summary>
    public static RhythmPart? PartOf(TrackRole role) => role switch
    {
        TrackRole.Melody => RhythmPart.Melody,
        TrackRole.Chords => RhythmPart.Chords,
        TrackRole.Bass => RhythmPart.Bass,
        TrackRole.Pad => RhythmPart.Pad,
        TrackRole.CounterMelody => RhythmPart.CounterMelody,
        TrackRole.Riff or TrackRole.RiffTwin => RhythmPart.Riff,
        TrackRole.Rhythm => RhythmPart.Rhythm,
        _ => null
    };

    /// <summary>The melody's busyness moved by the genre's.</summary>
    public static MelodyBusyness Of(Genre genre, MelodyBusyness busyness) =>
        new(Math.Clamp(busyness.Value + BusynessRange * genre.Shift(GenreChoices.Rhythms[RhythmPart.Melody], "busyness"), 0, 1));
}

/// <summary>A part whose rhythm a genre may move: a pitched part, or the drums of a role.</summary>
public enum RhythmPart
{
    Melody,
    Chords,
    Bass,
    Pad,
    CounterMelody,
    Riff,
    Rhythm,
    Ground,
    Backbeat,
    Time,
    Colour
}

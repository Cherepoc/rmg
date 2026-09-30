using System.Collections.Immutable;
using Rmg.Core.Events;
using Rmg.Core.Probabilities;

namespace Rmg.Core.Composition;

/// <summary>
///     The families a drum plays in: the drum kit, the percussion, or both, as the hand percussion, the tambourine, the
///     shakers and the clap, does; a section of percussion only plays the percussion's.
/// </summary>
[Flags]
public enum DrumFamily
{
    Kit = 1,
    Percussion = 2
}

/// <summary>A drum's role in the groove, which sets its fixed rhythm (<see cref="DrumRoles" />).</summary>
public enum DrumRole
{
    /// <summary>Grounds the groove, as the kick does, repeating its figure.</summary>
    Ground,

    /// <summary>Plays the backbeat, as the snare does.</summary>
    Backbeat,

    /// <summary>Keeps time, as the hi-hat does, faster, full and steady.</summary>
    Time,

    /// <summary>Colours the groove, as the toms, a crash ride or more percussion do, with no fixed rhythm of its own.</summary>
    Colour
}

/// <summary>
///     The roles the drums play in the groove. A drum's affinity for every role is data on it, its main role the heaviest:
///     the song draws the drum's role by them, and a section may draw it again, the lowest layer's winning
///     (<see cref="CompositionStateKinds.DrumRole" />); both lean to the roles not the drum's main one the less
///     conventional the rhythm, so that in a wild section any drum may take any role its affinities allow. A role sets
///     the drum's fixed rhythm, its part: the song's role in the drum's definition, and a section's other role as the
///     difference from the song's, as every part's state adds up.
/// </summary>
internal static class DrumRoles
{
    /// <summary>The layer of a role's part, as a trace tells it.</summary>
    public const string Layer = "Drum role";

    /// <summary>The chance a section draws a drum's role again, with no lean.</summary>
    public const double SectionChangeChance = 0.2;

    /// <summary>The fixed rhythm a role gives a drum, as its steps of the rhythm settings.</summary>
    /// <param name="PeriodPower">Its cycle's steps, down for a faster one.</param>
    /// <param name="PhaseRank">Its cycle's shift, by rank: 1 for half a cycle, as the backbeat's.</param>
    /// <param name="MaxRank">How many ranks finer than its cycle it plays: fewer for no weaker hits of its own.</param>
    /// <param name="Fullness">How full it plays.</param>
    /// <param name="Variation">How much less often it draws its cycles afresh, so that it keeps its figure.</param>
    /// <param name="GrooveLean">
    ///     How the drum's own layers lean to move its groove, its speed and its phase (<see cref="GrooveHold" />): -1 for
    ///     the backbeat, which the snare alone does not leave for double or half time while the kick and the hi-hat
    ///     play on, the drums' shared layers still moving the groove of them all.
    /// </param>
    public sealed record Part(int PeriodPower, int PhaseRank, int MaxRank, double Fullness, double Variation, double GrooveLean)
    {
        /// <summary>The part as state, times the sign given, so that a section can take one part and add another.</summary>
        public StateMap ToStateMap(int sign = 1)
        {
            IState[] states =
            [
                CompositionStateKinds.Rhythm.Period.Power.CreateState(sign * PeriodPower),
                CompositionStateKinds.Rhythm.Phase.Rank.CreateState(sign * PhaseRank),
                CompositionStateKinds.Rhythm.MaxRank.CreateState(sign * MaxRank),
                CompositionStateKinds.Rhythm.Fullness.CreateState(sign * Fullness),
                CompositionStateKinds.Rhythm.Variation.CreateState(sign * Variation)
            ];
            // a trace tells the role's part by its layer
            return StateMap.FromStates([..states.Where(x => !x.IsDefault).Select(x => StateTrace.IsRunning ? x.WithLayer(Layer) : x)]);
        }
    }

    /// <summary>
    ///     Every role's part: the ground repeats its figure; the backbeat is a cycle of half a bar shifted by half of it,
    ///     its main hits on beats 2 and 4, with no weaker hits of its own unless the rhythm layers add ghost notes, and it
    ///     keeps its figure; time plays faster than the other drums, full and steady; colour has no part of its own.
    /// </summary>
    public static ImmutableDictionary<DrumRole, Part> Parts { get; } = new Dictionary<DrumRole, Part>
    {
        [DrumRole.Ground] = new(0, 0, 0, 0, -0.4, 0),
        [DrumRole.Backbeat] = new(-1, 1, -2, 0, -0.3, -1),
        [DrumRole.Time] = new(-1, 0, 0, 0.35, -0.5, 0),
        [DrumRole.Colour] = new(0, 0, 0, 0, 0, 0)
    }.ToImmutableDictionary();

    /// <summary>
    ///     How far a role's groove lean moves the odds of a drum's own layers moving its speed or its phase: a lean of -1
    ///     makes them an eighth.
    /// </summary>
    public static Tilt GrooveHold { get; } = Tilt.Of(8, 1);

    /// <summary>A drum's own rhythm layer, its groove held as its role holds it (<see cref="Part.GrooveLean" />).</summary>
    public static RhythmLayer Hold(RhythmLayer layer, DrumRole role) => layer.LeanGroove(GrooveHold, Parts[role].GrooveLean);

    /// <summary>The role a drum's state gives it.</summary>
    public static DrumRole Of(StateMap drumStateMap) => (DrumRole)drumStateMap.GetStateValue(CompositionStateKinds.DrumRole).Value;

    /// <summary>
    ///     The weakest rank of the lead's beats a drum that doubles it plays: the backbeat's main hits, and time's beats
    ///     and 8ths, as a clap doubles the backbeat and a shaker the hi-hat's pulse.
    /// </summary>
    public static ImmutableDictionary<DrumRole, int> DoublingRanks { get; } = new Dictionary<DrumRole, int>
    {
        [DrumRole.Ground] = 0,
        [DrumRole.Backbeat] = 0,
        [DrumRole.Time] = 1,
        [DrumRole.Colour] = 0
    }.ToImmutableDictionary();

    /// <summary>A layer's role of a drum, at its depth, with no part.</summary>
    public static StateMap At(int depth, DrumRole role)
    {
        return StateMap.FromStates([CompositionStateKinds.DrumRole.CreateState(new LayerValue<int>(depth, (int)role))]);
    }

    /// <summary>The song's role of a drum, by its affinities, leaned by the rhythm's unconventionality, and its part.</summary>
    public static StateMap GenerateSong(IGenerationContext context, PercussionInstrumentDefinition drum, Tilt rhythm)
    {
        var role = Pick(context, drum, rhythm);
        return At(StateDepths.Song, role).MergeWith(Parts[role].ToStateMap());
    }

    /// <summary>
    ///     A section's role of a drum, now and then drawn again by its affinities, the likelier and the more to the roles
    ///     not its main one the less conventional the rhythm; the role and the difference of its part from the song's, or
    ///     none for the song's. Always two draws.
    /// </summary>
    public static StateMap DrawSection(IGenerationContext context, PercussionInstrumentDefinition drum, DrumRole songRole, Tilt rhythm)
    {
        var drawsAgain = context.TestProbability(rhythm.Chance(SectionChangeChance, 1));
        var role = Pick(context, drum, rhythm);
        if (!drawsAgain || role == songRole)
            return StateMap.Default;

        return At(StateDepths.Section, role).MergeWith(Parts[songRole].ToStateMap(-1)).MergeWith(Parts[role].ToStateMap());
    }

    private static DrumRole Pick(IGenerationContext context, PercussionInstrumentDefinition drum, Tilt rhythm)
    {
        var weights = rhythm.Weigh(drum.Roles.Where(x => x.Weight > 0), x => x == drum.MainRole ? 0 : 1);
        return context.Pick(weights);
    }
}

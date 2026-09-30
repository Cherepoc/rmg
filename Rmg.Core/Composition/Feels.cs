using System.Collections.Immutable;
using Rmg.Core.Events;
using Rmg.Core.Probabilities;

namespace Rmg.Core.Composition;

/// <summary>
///     The feels a rhythm plays in, by their step of <see cref="RhythmPeriod" />: straight, and for 3, 5, 7, 11 and 13 a
///     tuplet, the prime's notes over a power of two of the grid's, and a grouping, cycles of the prime's steps, such as
///     triplets and a dotted 8th's 3+3+2. The song draws its feel, and a section, a track's bar and a fill change the feel
///     they come to now and then, each by the feel facet of its unconventionality: the plainest song straight or in
///     threes, all of it, the wildest in any feel but straight, changing at every section, bar and fill.
/// </summary>
internal static class Feels
{
    /// <summary>Straight time, no tuplet or grouping.</summary>
    public const int Straight = 0;

    /// <summary>
    ///     How likely a feel is, for the song's and for a change: straight, and the threes now and then, at the plain
    ///     end; almost always straight at the middle, where a song in a tuplet is rare; every other feel as likely at the
    ///     wild end.
    /// </summary>
    public static ImmutableArray<(int Feel, ByConvention Weight)> Weights { get; } =
    [
        (Straight, new ByConvention(1, 0.95, 0)),
        (1, new ByConvention(0.08, 0.02, 1)),
        (-1, new ByConvention(0.08, 0.02, 1)),
        (-2, new ByConvention(0, 0.003, 1)),
        (2, new ByConvention(0, 0.003, 1)),
        (3, new ByConvention(0, 0.001, 1)),
        (-3, new ByConvention(0, 0.001, 1)),
        (-4, new ByConvention(0, 0.0005, 1)),
        (4, new ByConvention(0, 0.0005, 1)),
        (5, new ByConvention(0, 0.0005, 1)),
        (-5, new ByConvention(0, 0.0005, 1))
    ];

    /// <summary>The chance a section changes the song's feel, for every track.</summary>
    public static ByConvention SectionChange { get; } = new(0, 0.12, 1);

    /// <summary>The chance a track's bar changes the feel it comes to, a passage in another feel.</summary>
    public static ByConvention BarChange { get; } = new(0, 0.05, 1);

    /// <summary>The chance a fill changes the groove's feel, such as into triplets.</summary>
    public static ByConvention FillChange { get; } = new(0, 0.02, 1);

    /// <summary>A song's feel, at the feel facet of its unconventionality.</summary>
    public static int DrawSong(IGenerationContext context, double unconventionality)
    {
        return context.Pick(ByConvention.Weigh(Weights, unconventionality));
    }

    /// <summary>A layer's change of the feel it comes to, by its chance, another feel; none for keeping it.</summary>
    public static int? DrawChange(IGenerationContext context, int feel, ByConvention chance, double unconventionality)
    {
        return context.TestProbability(chance.At(unconventionality))
            ? context.Pick(ByConvention.Weigh(Weights.Where(x => x.Feel != feel), unconventionality))
            : null;
    }

    /// <summary>A layer's feel, at its depth, over those above it.</summary>
    public static StateMap At(int depth, int feel)
    {
        return StateMap.FromStates([CompositionStateKinds.Rhythm.Feel.CreateState(new LayerValue<int>(depth, feel))]);
    }

    /// <summary>The feel a state plays in: its lowest layer's, straight where none sets one.</summary>
    public static int Of(StateMap stateMap)
    {
        return stateMap.GetStateValue(CompositionStateKinds.Rhythm.Feel).Value;
    }
}

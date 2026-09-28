using System.Collections.Immutable;
using Rmg.Core.Events;
using Rmg.Core.Probabilities;

namespace Rmg.Core.Composition;

/// <summary>
///     The sound a drum that strikes plays steadily, its stroke, such as the snare's head or its cross-stick, or the
///     hi-hat closed, by its pedal or open: the song picks one, a section may change it and a bar of a later letter of
///     the phrase scheme may change the section's, the lowest layer's winning (<see cref="StateKinds.DrumStroke" />).
///     A stroke is picked by its sound's weight, leaned by its loudness and the energy, and a change is likelier the
///     heavier the new stroke against the current one, the less conventional the rhythm and the more the new stroke's
///     loudness goes the energy's way, so that the strokes keep to their weights over a song, and a quiet verse plays the
///     cross-stick now and then and a chorus the snare.
/// </summary>
public static class DrumStrokes
{
    /// <summary>The chance a section changes a drum's stroke from the song's, with no lean.</summary>
    public const double SectionChangeChance = 0.3;

    /// <summary>The chance a bar of a later letter changes a drum's stroke from its section's, with no lean.</summary>
    public const double BarChangeChance = 0.15;

    /// <summary>A layer's stroke, at its depth.</summary>
    public static StateMap At(int depth, int stroke)
    {
        return StateMap.FromStates([StateKinds.DrumStroke.CreateState(new LayerValue<int>(depth, stroke))]);
    }

    /// <summary>The song's stroke of a drum, by its sounds' weights; none for a drum without strokes.</summary>
    public static StateMap GenerateSong(IGenerationContext context, PercussionInstrumentDefinition drum)
    {
        return drum.HasStrokes ? At(StateDepths.Song, Pick(context, drum, Tilt.None, -1)) : StateMap.Default;
    }

    /// <summary>
    ///     A stroke other than the current one, now and then: a candidate picked by weight and leaned by the energy, and
    ///     taken by the chance given, its odds times the candidate's weight over the current one's, and leaned by how
    ///     unconventional the rhythm is and by how far the candidate's loudness goes the energy's way; none for keeping
    ///     the current one. Always two draws.
    /// </summary>
    public static int? DrawChange(
        IGenerationContext context,
        PercussionInstrumentDefinition drum,
        int current,
        double chance,
        Tilt rhythm,
        Tilt energy
    )
    {
        var candidate = Pick(context, drum, energy, current);
        var (to, from) = (drum.Sounds[candidate], drum.Sounds[current]);
        var lean = Math.Clamp(to.Loudness - from.Loudness, -1, 1);
        var weighed = new Tilt(Math.Log(to.Stroke / from.Stroke)).Chance(chance, 1);
        return context.TestProbability(rhythm.Chance(energy.Chance(weighed, lean), 1)) ? candidate : null;
    }

    private static int Pick(IGenerationContext context, PercussionInstrumentDefinition drum, Tilt energy, int except)
    {
        ImmutableArray<Weighted<int>> weights =
        [
            ..drum.Sounds
                .Select((x, i) => new Weighted<int>(i == except ? 0 : energy.Weigh(x.Stroke, x.Loudness), i))
                .Where(x => x.Weight > 0)
        ];
        return weights[Generators.WeightedIndex(weights)(context)].Value;
    }
}

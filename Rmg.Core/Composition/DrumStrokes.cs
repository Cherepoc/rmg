using System.Collections.Immutable;
using Rmg.Core.Events;
using Rmg.Core.Probabilities;
using Rmg.Core.Songs;

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
internal static class DrumStrokes
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

    /// <summary>The song's stroke of a drum, by its sounds' weights, a wild sound's never at the plain end; none for a drum without strokes.</summary>
    /// <param name="odds">How much likelier a genre has each sound, by its note number (<see cref="DrumSounds.OddsOf" />).</param>
    /// <param name="unconventionality">How far the song's rhythm strays, which a wild sound's weight leans by, as a fill's does (<see cref="FillSounds" />).</param>
    public static StateMap GenerateSong(IGenerationContext context, PercussionInstrumentDefinition drum, Func<int, double> odds, double unconventionality)
    {
        return drum.HasStrokes ? At(StateDepths.Song, Pick(context, drum, Tilt.None, -1, odds, unconventionality)) : StateMap.Default;
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
        double unconventionality,
        Tilt energy,
        Func<int, double> odds
    )
    {
        var candidate = Pick(context, drum, energy, current, odds, unconventionality);
        var (to, from) = (drum.Sounds[candidate], drum.Sounds[current]);
        var lean = Math.Clamp(to.Loudness - from.Loudness, -1, 1);
        var weighed = new Tilt(Math.Log(to.Stroke * odds(to.Code) / (from.Stroke * odds(from.Code)))).Chance(chance, 1);
        return context.TestProbability(RhythmicUnconventionality.Ends(energy.Chance(weighed, lean), 1).At(unconventionality)) ? candidate : null;
    }

    private static int Pick(IGenerationContext context, PercussionInstrumentDefinition drum, Tilt energy, int except, Func<int, double> odds, double unconventionality)
    {
        // a wild sound, the china's or the splash's, none at the plain end but where a genre has it likelier, and heavier at
        // the wild end
        double Stroke(DrumSound sound) =>
            new ByConvention(sound.Wildness > 0 && odds(sound.Code) <= 1 ? 0 : sound.Stroke, sound.Stroke, sound.Stroke * (1 + sound.Wildness)).At(unconventionality);
        ImmutableArray<Weighted<int>> weights =
        [
            ..drum.Sounds
                .Select((x, i) => new Weighted<int>(i == except ? 0 : energy.Weigh(Stroke(x) * odds(x.Code), x.Loudness), i))
                .Where(x => x.Weight > 0)
        ];
        return context.Pick(weights);
    }
}

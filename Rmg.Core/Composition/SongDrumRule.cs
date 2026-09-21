using System.Collections.Immutable;
using Rmg.Core.Probabilities;

namespace Rmg.Core.Composition;

public delegate void SongDrumStep(IGenerationContext context, List<PercussionInstrumentDefinition> selectedDrums);

/// <summary>
///     Decides which drums of a group are available in a song. A rule is a sequence of steps that build up the
///     selection, so a later step can depend on what the earlier ones chose.
/// </summary>
public sealed class SongDrumRule
{
    private readonly ImmutableArray<SongDrumStep> _steps;

    public SongDrumRule(ImmutableArray<SongDrumStep> steps)
    {
        _steps = steps;
    }

    private SongDrumRule()
    {
    }

    /// <summary>Every drum of the group is available.</summary>
    public static SongDrumRule AllDrums { get; } = new();

    public ImmutableArray<PercussionInstrumentDefinition> Select(
        IGenerationContext context,
        ImmutableArray<PercussionInstrumentDefinition> groupDrums
    )
    {
        if (_steps.IsDefault)
            return groupDrums;

        var selectedDrums = new List<PercussionInstrumentDefinition>();
        foreach (var step in _steps)
            step(context, selectedDrums);

        if (selectedDrums.Except(groupDrums).Any())
            throw new InvalidOperationException("A song drum rule selected a drum that is not in the group.");

        // keep the group order so that the result only depends on what was picked
        return [..groupDrums.Where(selectedDrums.Contains)];
    }

    /// <summary>The drums are always available.</summary>
    public static SongDrumStep Always(params PercussionInstrumentDefinition[] drums)
    {
        return (_, selectedDrums) => selectedDrums.AddRange(drums);
    }

    /// <summary>Exactly one of the drums is available, the heavier ones being more likely.</summary>
    public static SongDrumStep OneOf(params PercussionInstrumentDefinition[] drums)
    {
        return Pool(1, 1, drums);
    }

    /// <summary>Between <paramref name="min" /> and <paramref name="max" /> of the drums are available, possibly none.</summary>
    public static SongDrumStep Pool(int min, int max, params PercussionInstrumentDefinition[] drums)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(min);
        ArgumentOutOfRangeException.ThrowIfLessThan(max, min);
        ArgumentOutOfRangeException.ThrowIfGreaterThan(max, drums.Length);

        return (context, selectedDrums) =>
        {
            var count = context.GenerateInt(min, max + 1);
            var pickedDrums = DrumKitGenerator.PickWeighted(
                context,
                drums.Select(x => new Weighted<PercussionInstrumentDefinition>(x.Weight, x)),
                count
            );
            selectedDrums.AddRange(pickedDrums);
        };
    }

    /// <summary>
    ///     The drum is available with the given probability, unless one of <paramref name="unlessSelected" /> has
    ///     already been selected by the earlier steps.
    /// </summary>
    public static SongDrumStep Optional(
        PercussionInstrumentDefinition drum,
        double probability,
        params PercussionInstrumentDefinition[] unlessSelected
    )
    {
        return (context, selectedDrums) =>
        {
            // the probability is tested anyway to keep the random sequence independent of the selection
            var isAvailable = context.TestProbability(probability);
            if (isAvailable && !selectedDrums.Contains(drum) && !selectedDrums.Intersect(unlessSelected).Any())
                selectedDrums.Add(drum);
        };
    }
}

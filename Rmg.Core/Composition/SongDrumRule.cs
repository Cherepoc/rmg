using System.Collections.Immutable;
using Rmg.Core.Probabilities;

namespace Rmg.Core.Composition;

/// <param name="pool">A drum's weight in a pool, such as a genre's, in place of its own, from 0, out of it, to 1; none for the drums as they are.</param>
public delegate void SongDrumStep(IGenerationContext context, List<PercussionInstrumentDefinition> selectedDrums, Func<PercussionInstrumentDefinition, double>? pool);

/// <summary>
///     Decides which drums of a group are available in a song. A rule is a sequence of steps that build up the
///     selection, so a later step can depend on what the earlier ones chose. A drum out of the pool given is never
///     selected, and one in it as likely as its weight there.
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
        ImmutableArray<PercussionInstrumentDefinition> groupDrums,
        Func<PercussionInstrumentDefinition, double>? pool
    )
    {
        if (_steps.IsDefault)
            return [..groupDrums.Where(x => pool is null || pool(x) > 0)];

        var selectedDrums = new List<PercussionInstrumentDefinition>();
        foreach (var step in _steps)
            step(context, selectedDrums, pool);

        if (selectedDrums.Except(groupDrums).Any())
            throw new InvalidOperationException("A song drum rule selected a drum that is not in the group.");

        // keep the group order so that the result only depends on what was picked
        return [..groupDrums.Where(selectedDrums.Contains)];
    }

    /// <summary>The drums are always available.</summary>
    public static SongDrumStep Always(params PercussionInstrumentDefinition[] drums)
    {
        return (_, selectedDrums, pool) => selectedDrums.AddRange(drums.Where(x => pool is null || pool(x) > 0));
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

        return (context, selectedDrums, pool) =>
        {
            var count = context.GenerateInt(min, max + 1);
            var pickedDrums = DrumKitGenerator.PickWeighted(
                context,
                drums.Where(x => pool is null || pool(x) > 0).Select(x => new Weighted<PercussionInstrumentDefinition>(pool?.Invoke(x) ?? x.Weight, x)),
                count
            );
            selectedDrums.AddRange(pickedDrums);
        };
    }

    /// <summary>
    ///     Extra drums beside a group's own, as <see cref="Pool" /> picks them for the songs; a genre's each kept as often as
    ///     it weighs it against its heaviest, so that a tambourine it names lightly plays now and then rather than in every
    ///     song, and one it names not at all never.
    /// </summary>
    public static SongDrumStep Extras(int min, int max, params PercussionInstrumentDefinition[] drums)
    {
        var picks = Pool(min, max, drums);
        return (context, selectedDrums, pool) =>
        {
            // picked as the songs pick them, and a genre's each kept by its weight, once
            var picked = new List<PercussionInstrumentDefinition>();
            picks(context, picked, null);
            selectedDrums.AddRange(pool is null ? picked : picked.Where(x => context.TestProbability(Math.Min(1, pool(x)))));
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
        return (context, selectedDrums, pool) =>
        {
            // one draw whatever the selection, so that the sequence stays as it is; a pool's weight, as a share of its heaviest,
            // the chance in place of the songs', a jazz genre's ride at its heaviest always
            var isAvailable = context.TestProbability(pool is null ? probability : Math.Min(1, pool(drum)));
            if (isAvailable && (pool is null || pool(drum) > 0) && !selectedDrums.Contains(drum) && !selectedDrums.Intersect(unlessSelected).Any())
                selectedDrums.Add(drum);
        };
    }
}

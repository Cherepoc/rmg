using System.Collections.Immutable;
using System.Diagnostics;
using Rmg.Core.Events;

namespace Rmg.Core.Composition;

[DebuggerDisplay("PercussionInstrumentDefinition {Name}")]
public sealed class PercussionInstrumentDefinition
{
    public PercussionInstrumentDefinition(
        string name,
        ImmutableArray<int> articulationCodes,
        double weight,
        Func<StateMapBuilder, StateMapBuilder>? configureStateMap = null
    )
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(name);
        if (articulationCodes.Length == 0)
            throw new ArgumentException("Articulation codes cannot be empty.", nameof(articulationCodes));
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(weight);

        Name = name;
        ArticulationCodes = articulationCodes;
        Weight = weight;
        ConfigureStateMap = configureStateMap ?? (builder => builder);
    }

    public string Name { get; }

    public ImmutableArray<int> ArticulationCodes { get; }

    /// <summary>The number of one of the drum's sounds, counted from 1, as a note names it outright.</summary>
    public int GetArticulationIndex(int code)
    {
        var index = ArticulationCodes.IndexOf(code);
        if (index < 0)
            throw new ArgumentException($"The drum {Name} has no sound {code}.", nameof(code));
        return index + 1;
    }

    /// <summary>How likely the drum is to be chosen among the other drums of its group.</summary>
    public double Weight { get; }

    /// <summary>Adds drum-specific fixed state to a track state map, on top of the group state.</summary>
    public Func<StateMapBuilder, StateMapBuilder> ConfigureStateMap { get; }
}

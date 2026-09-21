using System.Collections.Immutable;
using System.Diagnostics;
using Rmg.Core.Events;

namespace Rmg.Core.Composition;

/// <summary>
///     A set of drums that are too similar or too busy to be played all together. Only
///     <see cref="MaxActiveDrums" /> of them sound in a section, and only some groups are active in a section.
/// </summary>
[DebuggerDisplay("DrumGroup {Name}")]
public sealed class DrumGroup
{
    public DrumGroup(
        string name,
        ImmutableArray<PercussionInstrumentDefinition> drums,
        double selectionWeight,
        int maxActiveDrums,
        bool isAlwaysOn = false,
        Func<StateMapBuilder, StateMapBuilder>? configureStateMap = null,
        SongDrumRule? songRule = null
    )
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(name);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(selectionWeight);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(maxActiveDrums);
        if (drums.IsDefaultOrEmpty)
            throw new ArgumentException("Drums cannot be empty.", nameof(drums));

        Name = name;
        Drums = drums;
        SelectionWeight = selectionWeight;
        MaxActiveDrums = Math.Min(maxActiveDrums, drums.Length);
        IsAlwaysOn = isAlwaysOn;
        ConfigureStateMap = configureStateMap ?? (builder => builder);
        SongRule = songRule ?? SongDrumRule.AllDrums;
    }

    public string Name { get; }

    public ImmutableArray<PercussionInstrumentDefinition> Drums { get; }

    /// <summary>How likely the group is to be chosen among the other optional groups.</summary>
    public double SelectionWeight { get; }

    public int MaxActiveDrums { get; }

    /// <summary>Always-on groups are active in every section and do not count towards the optional groups.</summary>
    public bool IsAlwaysOn { get; }

    /// <summary>Adds group-specific fixed state, shared by all the drums of the group, to a track state map.</summary>
    public Func<StateMapBuilder, StateMapBuilder> ConfigureStateMap { get; }

    /// <summary>Decides which of the drums are available in a song.</summary>
    public SongDrumRule SongRule { get; }
}

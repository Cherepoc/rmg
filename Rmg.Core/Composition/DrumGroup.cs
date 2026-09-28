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
        SongDrumRule? songRule = null,
        double grooveChance = 1,
        bool holdsARole = false
    )
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(name);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(selectionWeight);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(maxActiveDrums);
        ArgumentOutOfRangeException.ThrowIfNegative(grooveChance);
        ArgumentOutOfRangeException.ThrowIfGreaterThan(grooveChance, 1);
        if (drums.IsDefaultOrEmpty)
            throw new ArgumentException("Drums cannot be empty.", nameof(drums));

        Name = name;
        Drums = drums;
        SelectionWeight = selectionWeight;
        MaxActiveDrums = Math.Min(maxActiveDrums, drums.Length);
        IsAlwaysOn = isAlwaysOn;
        ConfigureStateMap = configureStateMap ?? (builder => builder);
        SongRule = songRule ?? SongDrumRule.AllDrums;
        GrooveChance = grooveChance;
        HoldsARole = holdsARole;
    }

    /// <summary>
    ///     Whether the group's drum holds a role in the groove that a bar must keep, as the kick grounds it, the snare
    ///     plays the backbeat and the timekeepers keep time: it never sits out a bar, but may change its stroke there
    ///     (<see cref="DrumPresence" />); the other groups colour the groove, and come and go.
    /// </summary>
    public bool HoldsARole { get; }

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

    /// <summary>
    ///     The chance that an optional group is among those a section picks its groove from. A group that mostly plays
    ///     elsewhere, such as the toms in fills, has a low one, so that it grooves in few sections however few other
    ///     groups the song has.
    /// </summary>
    public double GrooveChance { get; }
}

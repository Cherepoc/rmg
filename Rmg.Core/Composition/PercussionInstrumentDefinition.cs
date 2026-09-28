using System.Collections.Immutable;
using System.Diagnostics;
using Rmg.Core.Events;
using Rmg.Core.Probabilities;
using Rmg.Core.Songs;

namespace Rmg.Core.Composition;

[DebuggerDisplay("PercussionInstrumentDefinition {Name}")]
public sealed class PercussionInstrumentDefinition
{
    public PercussionInstrumentDefinition(
        string name,
        ImmutableArray<DrumSound> sounds,
        double weight,
        Func<StateMapBuilder, StateMapBuilder>? configureStateMap = null,
        double loudness = 0,
        bool walks = false,
        ImmutableArray<Weighted<DrumRole>> roles = default,
        double doubling = 0,
        DrumFamily family = DrumFamily.Kit
    )
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(name);
        if (sounds.Length == 0)
            throw new ArgumentException("A drum has at least one sound.", nameof(sounds));
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(weight);
        ArgumentOutOfRangeException.ThrowIfLessThan(loudness, -1);
        ArgumentOutOfRangeException.ThrowIfGreaterThan(loudness, 1);

        Name = name;
        Sounds = sounds;
        ArticulationCodes = [..sounds.Select(x => x.Code)];
        Weight = weight;
        ConfigureStateMap = configureStateMap ?? (builder => builder);
        Loudness = loudness;
        Walks = walks;
        Roles = roles.IsDefaultOrEmpty ? [new Weighted<DrumRole>(1, DrumRole.Colour)] : roles;
        Doubling = doubling;
        Family = family;
    }

    /// <summary>The families the drum plays in, the drum kit, the percussion or both.</summary>
    public DrumFamily Family { get; }

    /// <summary>
    ///     How likely the drum is to double the lead of its role in a section, such as the clap on the snare's backbeat,
    ///     among the drums that may (<see cref="DrumKitGenerator.SelectKit" />); 0 for never.
    /// </summary>
    public double Doubling { get; }

    /// <summary>
    ///     The drum's affinity for every role in the groove (<see cref="DrumRoles" />), its main role the heaviest; a drum
    ///     with none given colours it.
    /// </summary>
    public ImmutableArray<Weighted<DrumRole>> Roles { get; }

    /// <summary>The drum's heaviest role, which it fills in a section's kit (<see cref="DrumKitGenerator.SelectKit" />).</summary>
    public DrumRole MainRole => Roles.MaxBy(x => x.Weight).Value;

    /// <summary>
    ///     Whether the drum walks its sounds from note to note, as the toms and the congas walk their pitches; a drum
    ///     that does not strikes one sound steadily, its stroke (<see cref="DrumStrokes" />), such as the snare's head.
    /// </summary>
    public bool Walks { get; }

    /// <summary>Whether the groove chooses among the drum's sounds as its stroke: a drum that strikes, of more than one.</summary>
    public bool HasStrokes => !Walks && Sounds.Count(x => x.Stroke > 0) > 1;

    public string Name { get; }

    public ImmutableArray<DrumSound> Sounds { get; }

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

    /// <summary>
    ///     How the drum leans, from -1, a quiet drum such as the cross-stick, through 0 to 1, a loud one such as the
    ///     crash, which makes it likelier in a section of more energy and less likely in one of less.
    /// </summary>
    public double Loudness { get; }

    /// <summary>Adds drum-specific fixed state to a track state map, on top of the group state.</summary>
    public Func<StateMapBuilder, StateMapBuilder> ConfigureStateMap { get; }
}

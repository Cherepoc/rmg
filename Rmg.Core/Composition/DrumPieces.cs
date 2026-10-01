using System.Collections.Immutable;

namespace Rmg.Core.Composition;

/// <summary>
///     A drum a song may play, as a request names it: its name, as it is shown, its group's, and the General MIDI notes it
///     sounds.
/// </summary>
public sealed record DrumPiece(string Name, string Label, string Group, ImmutableArray<int> Notes);

/// <summary>
///     The drums a song may play, every one of every group, by the names a request gives them in, which a song may be given
///     as a set in place of those it draws (<see cref="SongOverrides.DrumPieces" />).
/// </summary>
public static class DrumPieces
{
    public static ImmutableArray<DrumPiece> All { get; } =
    [
        ..DrumGroups.All.SelectMany(group => group.Drums.Select(drum => new DrumPiece(NameOf(drum), drum.Name, group.Name, [..drum.Sounds.Select(x => x.Code).Distinct()])))
    ];

    private static readonly ImmutableDictionary<string, PercussionInstrumentDefinition> ByName =
        DrumGroups.AllDrums.ToImmutableDictionary(NameOf, x => x);

    /// <summary>A drum's name as a request gives it: its own, in camel case, "hiHat", "woodBlock".</summary>
    public static string NameOf(PercussionInstrumentDefinition drum)
    {
        var words = drum.Name.Split(' ', '-');
        return string.Concat(words.Select((word, i) => i == 0 ? word.ToLowerInvariant() : char.ToUpperInvariant(word[0]) + word[1..].ToLowerInvariant()));
    }

    /// <summary>Whether a name is a drum's.</summary>
    public static bool IsPiece(string name) => ByName.ContainsKey(name);

    /// <summary>The drums named, in the order of the groups.</summary>
    internal static ImmutableArray<PercussionInstrumentDefinition> Of(IEnumerable<string> names)
    {
        var named = names.Select(x => ByName[x]).ToHashSet();
        return [..DrumGroups.AllDrums.Where(named.Contains)];
    }

    /// <summary>
    ///     The setup the drums make: percussion alone with none of the kit's own drums, the kit and percussion with a drum of
    ///     the percussion group beside the kit's, and the kit otherwise, as <see cref="DrumSetups" /> draws them.
    /// </summary>
    internal static DrumSetup SetupOf(IEnumerable<PercussionInstrumentDefinition> drums)
    {
        var all = drums.ToArray();
        if (all.All(x => x.Family != DrumFamily.Kit))
            return DrumSetup.Percussion;
        return all.Any(DrumGroups.Percussion.Drums.Contains) ? DrumSetup.KitAndPercussion : DrumSetup.Kit;
    }

    /// <summary>The setup the named drums make (<see cref="SetupOf(IEnumerable{PercussionInstrumentDefinition})" />).</summary>
    public static DrumSetup SetupOf(IEnumerable<string> names) => SetupOf(Of(names));
}

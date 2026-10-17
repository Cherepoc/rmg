using System.Collections.Immutable;
using Rmg.Core.Probabilities;
using Rmg.Core.Songs;

namespace Rmg.Core.Composition;

/// <summary>An effect a player puts in one part's way, the one a song may have: none, a wah or a phaser.</summary>
public enum InsertionKind
{
    None,

    /// <summary>A wah sweeping a guitar's or a clavinet's sound, as a funk player's pedal does.</summary>
    Wah,

    /// <summary>A phaser swirling a held sound, strings', a pad's, an electric piano's or a guitar's.</summary>
    Phaser
}

/// <summary>A song's insertion effect and the part that plays through it.</summary>
public readonly record struct Insertion(InsertionKind Kind, TrackRole Part);

/// <summary>
///     The insertion effect a song plays one part through, as a Roland GS player has one for all its parts: a wah on a
///     guitar or a clavinet comping, a phaser on held chords; a genre's, and a wild song's now and then, drawn for the
///     song by its sound facet, and the part among those it suits.
/// </summary>
internal static class Insertions
{
    public static ImmutableArray<(InsertionKind Kind, ByConvention Weight)> Kinds { get; } =
    [
        (InsertionKind.None, new ByConvention(1, 1, 0.5)),
        (InsertionKind.Wah, new ByConvention(0, 0, 0.2)),
        (InsertionKind.Phaser, new ByConvention(0, 0, 0.3))
    ];

    /// <summary>An insertion effect as a genre's page shows it.</summary>
    public static string LabelOf(InsertionKind kind) => kind switch
    {
        InsertionKind.Wah => "A wah",
        InsertionKind.Phaser => "A phaser",
        _ => "None"
    };

    /// <summary>The effect's Roland GS type, its two bytes.</summary>
    public static (byte Msb, byte Lsb) TypeOf(InsertionKind kind) => kind switch
    {
        InsertionKind.Wah => (0x01, 0x21),
        InsertionKind.Phaser => (0x01, 0x20),
        _ => throw new ArgumentOutOfRangeException(nameof(kind), kind, "No insertion effect.")
    };

    /// <summary>Whether a part comps or holds chords on an instrument the effect suits.</summary>
    public static bool Suits(InsertionKind kind, TrackRole role, int program) => kind switch
    {
        InsertionKind.Wah => role is TrackRole.Rhythm or TrackRole.Chords or TrackRole.Riff && InstrumentTraits.Has(program, InstrumentTrait.Wahs),
        InsertionKind.Phaser => role is TrackRole.Pad or TrackRole.Chords or TrackRole.Rhythm && InstrumentTraits.Has(program, InstrumentTrait.Phases),
        _ => false
    };

    /// <summary>The song's insertion effect and its part, none where it draws none or no part it plays suits it.</summary>
    /// <param name="parts">The parts the song plays and their instruments.</param>
    public static Insertion? Draw(IGenerationContext context, double sound, Genre genre, IEnumerable<(TrackRole Role, int Program)> parts)
    {
        var kind = context.Pick(ByConvention.Weigh(Kinds, sound, genre.Pool<InsertionKind>(GenreChoices.Insertion)));
        ImmutableArray<Weighted<TrackRole>> suited = [..parts.Where(x => Suits(kind, x.Role, x.Program)).Select(x => new Weighted<TrackRole>(1, x.Role))];
        return suited.IsEmpty ? null : new Insertion(kind, context.Pick(suited));
    }
}

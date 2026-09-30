using System.Collections.Immutable;
using Rmg.Core.Probabilities;

namespace Rmg.Core.Composition;

/// <summary>What a section does in its song's form.</summary>
public enum SectionRole
{
    /// <summary>A section of a song whose form is its own, with no role of a convention's.</summary>
    Free,
    Verse,
    PreChorus,
    Chorus,
    Bridge
}

/// <summary>A song's sections in its order, and the role each plays.</summary>
public sealed record SongStructure(ImmutableArray<int> SectionIds, ImmutableDictionary<int, SectionRole> Roles);

/// <summary>
///     A song's form: most often one of the forms songs are written in, its roles in their order, a verse and a
///     chorus coming back, a pre-chorus before the chorus and a bridge before the last chorus, a section for every
///     role; and otherwise a form of its own (<see cref="SongStructureGenerator" />), whose sections have no role, the
///     likelier the less conventional the song's rhythm.
/// </summary>
internal static class SongForms
{
    /// <summary>The chance a song takes one of the forms, at a rhythm of middling conventionality.</summary>
    public const double FormChance = 0.7;

    /// <summary>The forms, as their roles in order, and how often each is drawn.</summary>
    public static ImmutableArray<Weighted<ImmutableArray<SectionRole>>> Forms { get; } =
    [
        new(0.35, Parse("VCVCBC")),
        new(0.25, Parse("VPCVPCBC")),
        new(0.15, Parse("VCVCBVC")),
        new(0.1, Parse("VCVC")),
        new(0.1, Parse("CVCVBC")),
        new(0.05, Parse("VPCVPC"))
    ];

    /// <param name="context">The sequence the form is drawn from.</param>
    /// <param name="freeContext">The sequence a form of the song's own is drawn from (<see cref="SongStructureGenerator" />).</param>
    /// <param name="unconventionality">
    ///     The form facet of the song's unconventionality: the plainest song always in one of the forms, the wildest always
    ///     in one of its own.
    /// </param>
    public static SongStructure Generate(IGenerationContext context, IGenerationContext freeContext, double unconventionality)
    {
        var isConventional = context.TestProbability(RhythmicUnconventionality.Ends(FormChance, -1).At(unconventionality));
        var form = context.Pick(Forms);
        if (!isConventional)
        {
            var free = SongStructureGenerator.Generate(freeContext).SelectMany(part => part.SectionIds).ToImmutableArray();
            return new SongStructure(free, free.Distinct().ToImmutableDictionary(x => x, _ => SectionRole.Free));
        }

        // a section for every role, numbered in the order the roles first play
        var ids = form.Distinct().Select((role, id) => (role, id)).ToDictionary(x => x.role, x => x.id);
        return new SongStructure([..form.Select(x => ids[x])], ids.ToImmutableDictionary(x => x.Value, x => x.Key));
    }

    private static ImmutableArray<SectionRole> Parse(string roles)
    {
        return
        [
            ..roles.Select(x => x switch
            {
                'V' => SectionRole.Verse,
                'P' => SectionRole.PreChorus,
                'C' => SectionRole.Chorus,
                'B' => SectionRole.Bridge,
                _ => throw new ArgumentException($"No role is written '{x}'.", nameof(roles))
            })
        ];
    }
}
